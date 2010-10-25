using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

/// <summary>
/// Reads and writes JSON data to a single restd resource file
/// </summary>
/// <remarks>
/// Maintains header and index information.
/// Calculates indexes (if any) during construction.
/// </remarks>
public class Restd
{
  private static object SyncRoot = new object();

  private string EntryPrefixFormat = @"{{ ""_restd"": ""{0}/{1}"", ";

  private string _restdFile = "";
  private string _serviceUri = "";
  private string _resourcePath = "";
  private int _byteOrderMarkSize = 3;
  private int _blockSize = -1;
  private HttpStatusCode _constructorStatus = HttpStatusCode.OK;

  public Restd(string restdFile, string serviceUri)
  {
    HttpStatusCode status = HttpStatusCode.OK;
    _serviceUri = serviceUri;

    if (serviceUri == null)
    {
      serviceUri = "";
    }

    if (!File.Exists(restdFile))
    {
      status = HttpStatusCode.NotFound;
    }

    if (status == HttpStatusCode.OK)
    {
      _resourcePath = "/" + Path.GetFileNameWithoutExtension(restdFile);
      _restdFile = restdFile;

      status = ReadHeader();
    }

    _constructorStatus = status;
  }

  /// <summary>
  /// Retrieve an array of entires and write them to an output stream
  /// </summary>
  public HttpStatusCode Query(string query, TextWriter output)
  {
    HttpStatusCode status = _constructorStatus;

    FileStream resourceStream = null;

    try
    {
      string[] filters;
      status = ParseQuery(query, out filters);

      if (status == HttpStatusCode.OK)
      {
        resourceStream = new FileStream(_restdFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        if (resourceStream == null)
        {
          status = HttpStatusCode.InternalServerError;
        }
      }

      char[] entryBuffer = null;

      if (status == HttpStatusCode.OK)
      {
        resourceStream.Seek(64 + _byteOrderMarkSize, SeekOrigin.Begin);
        StreamReader resourceReader = new StreamReader(resourceStream);

        entryBuffer = new char[_blockSize];

        output.Write("[");

        int index = 0, total = 0;
        int bytesRead = resourceReader.Read(entryBuffer, 0, _blockSize);
        while (bytesRead == _blockSize)
        {
          if (entryBuffer[0] == '{')
          {
            if (filters == null)
            {
              if (total > 0)
              {
                output.Write(',');
              }

              output.Write(EntryPrefixFormat, _resourcePath, index);

              WriteEntryProperties(entryBuffer, output);

              output.Write('}');

              total++;
            }
            else
            {
              StringBuilder entryBuilder = new StringBuilder();
              StringWriter entryWriter = new StringWriter(entryBuilder);

              entryWriter.Write(EntryPrefixFormat, _resourcePath, index);
              WriteEntryProperties(entryBuffer, entryWriter);
              entryWriter.Write('}');

              entryWriter.Close();
              string entry = entryBuilder.ToString();

              if (IsInFilter(entry, filters))
              {
                if (total > 0)
                {
                  output.Write(",");
                }

                output.Write(entry);

                total++;
              }
            }
          }

          index++;
          bytesRead = resourceReader.Read(entryBuffer, 0, _blockSize);
        }

        output.Write("]");
      }
    }
    catch (Exception)
    {
      status = HttpStatusCode.InternalServerError;
    }
    finally
    {
      if (resourceStream != null)
      {
        resourceStream.Close();
      }
    }

    return status;
  }

  /// <summary>
  /// Retrieve a single entry and write it to an output stream
  /// </summary>
  public HttpStatusCode GetItem(int key, TextWriter output)
  {
    HttpStatusCode status = _constructorStatus;

    FileStream resourceStream = null;

    try
    {
      if (status == HttpStatusCode.OK)
      {
        resourceStream = new FileStream(_restdFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        if (resourceStream == null)
        {
          status = HttpStatusCode.InternalServerError;
        }
      }

      if (status == HttpStatusCode.OK)
      {
        FileInfo resourceInfo = new FileInfo(_restdFile);
        long entityCount = (resourceInfo.Length - 64) / _blockSize;
        if (key >= entityCount)
        {
          status = HttpStatusCode.NotFound;
        }
      }

      char[] entryBuffer = null;

      if (status == HttpStatusCode.OK)
      {
        entryBuffer = new char[_blockSize];
        resourceStream.Seek(64 + _byteOrderMarkSize + key * _blockSize, SeekOrigin.Begin);

        StreamReader resourceReader = new StreamReader(resourceStream);
        int bytesRead = resourceReader.Read(entryBuffer, 0, _blockSize);
        if (!(bytesRead == _blockSize && entryBuffer[0] == '{'))
        {
          status = HttpStatusCode.NotFound;
        }
      }

      if (status == HttpStatusCode.OK)
      {
        output.Write(EntryPrefixFormat, _resourcePath, key);

        WriteEntryProperties(entryBuffer, output);

        output.Write("}");
      }
    }
    catch (Exception ex)
    {
      Trace.WriteLine(ex.Message);
      status = HttpStatusCode.InternalServerError;
    }
    finally
    {
      if (resourceStream != null)
      {
        resourceStream.Close();
      }
    }

    return status;
  }

  public HttpStatusCode PostItem(string entityData, out int? key)
  {
    HttpStatusCode status = HttpStatusCode.OK;

    FileStream resourceStream = null;
    key = null;

    try
    {
      if (entityData.Length > _blockSize)
      {
        status = HttpStatusCode.RequestEntityTooLarge;
      }
      else if (entityData[0] != '{' || entityData[entityData.Length - 1] != '}')
      {
        // Really, any valid JSON should be allowed
        status = HttpStatusCode.BadRequest;
      }

      if (status == HttpStatusCode.OK)
      {
        lock (SyncRoot)
        {
          FileInfo resourceInfo = new FileInfo(_restdFile);
          long entityCount = (resourceInfo.Length - 64) / _blockSize;
          long insertOffset = 64 + _byteOrderMarkSize + entityCount * _blockSize;

          resourceStream = new FileStream(_restdFile, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
          long insertPos = resourceStream.Seek(insertOffset, SeekOrigin.Begin);

          StreamWriter resourceWriter = new StreamWriter(resourceStream);
          resourceWriter.Write(entityData);
          resourceWriter.Write(",");
          resourceWriter.Write(new string(' ', _blockSize - entityData.Length - 1));
          resourceWriter.Write("null]}");
          resourceWriter.Flush();
          resourceStream.Close();
          resourceStream = null;

          key = (int)entityCount;
        }

      }
    }
    catch (Exception ex)
    {
      Trace.WriteLine(ex.Message);
      status = HttpStatusCode.InternalServerError;
    }
    finally
    {
      if (resourceStream != null)
      {
        resourceStream.Close();
      }
    }

    return status;
  }

  public HttpStatusCode PutItem(int key, string entityData)
  {
    HttpStatusCode status = HttpStatusCode.OK;

    FileStream resourceStream = null;

    try
    {
      if (entityData.Length > _blockSize)
      {
        status = HttpStatusCode.RequestEntityTooLarge;
      }
      else if (entityData[0] != '{' || entityData[entityData.Length - 1] != '}')
      {
        status = HttpStatusCode.BadRequest;
      }

      if (status == HttpStatusCode.OK)
      {
        lock (SyncRoot)
        {
          FileInfo resourceInfo = new FileInfo(_restdFile);
          long entityCount = (resourceInfo.Length - 64) / _blockSize;

          if (key >= entityCount)
          {
            status = HttpStatusCode.NotFound;
          }
          else
          {
            long insertOffset = 64 + _byteOrderMarkSize + key * _blockSize;

            resourceStream = new FileStream(_restdFile, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

            long insertPos = resourceStream.Seek(insertOffset, SeekOrigin.Begin);

            if (resourceStream.ReadByte() != (byte)'{')
            {
              status = HttpStatusCode.NotFound;
            }
            else
            {
              insertPos = resourceStream.Seek(-1, SeekOrigin.Current);

              StreamWriter resourceWriter = new StreamWriter(resourceStream);
              resourceWriter.Write(entityData);
              resourceWriter.Write(",");
              resourceWriter.Write(new string(' ', _blockSize - entityData.Length - 1));
              resourceWriter.Flush();
              resourceStream.Close();
              resourceStream = null;
            }
          }
        }
      }
    }
    catch (Exception ex)
    {
      Trace.WriteLine(ex.Message);
      status = HttpStatusCode.InternalServerError;
    }

    return status;
  }

  public HttpStatusCode DeleteItem(int key)
  {
    HttpStatusCode status = HttpStatusCode.OK;

    FileStream resourceStream = null;

    try
    {
      lock (SyncRoot)
      {
        FileInfo resourceInfo = new FileInfo(_restdFile);
        long entityCount = (resourceInfo.Length - 64) / _blockSize;

        if (key < entityCount)
        {
          long insertOffset = 64 + _byteOrderMarkSize + key * _blockSize;

          resourceStream = new FileStream(_restdFile, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

          long insertPos = resourceStream.Seek(insertOffset, SeekOrigin.Begin);

          StreamWriter resourceWriter = new StreamWriter(resourceStream);
          resourceWriter.Write(new string(' ', _blockSize));
          resourceWriter.Flush();
          resourceStream.Close();
          resourceStream = null;
        }
      }
    }
    catch (Exception ex)
    {
      Trace.WriteLine(ex.Message);
      status = HttpStatusCode.InternalServerError;
    }

    return status;
  }

  private bool IsInFilter(string entry, string[] filters)
  {
    bool ok = true;

    foreach (string filter in filters)
    {
      if (entry.IndexOf(filter, StringComparison.InvariantCultureIgnoreCase) == -1)
      {
        ok = false;
        break;
      }
    }

    return ok;
  }

  private HttpStatusCode ParseQuery(string query, out string[] filters)
  {
    HttpStatusCode status = HttpStatusCode.OK;

    filters = null;
    List<string> filterList = new List<string>();

    if (!String.IsNullOrEmpty(query))
    {
      query = query.Replace("|", "");
      Regex internalQuoteRegex = new Regex("\"\"(?<quoted>[^\"]+)\"\"", RegexOptions.ExplicitCapture);
      query = internalQuoteRegex.Replace(query, new MatchEvaluator(e => "{quoted}" + e.Groups["quoted"].Value + "{quoted}"));

      Regex termsRegex = new Regex("\"[^\"]+?\"|[^\"\\s]+");
      MatchCollection terms = termsRegex.Matches(query);

      foreach (Match term in terms)
      {
        filterList.Add(term.Groups[0].Value.Replace("\"", "").Replace("{quoted}", "\""));
      }

    }

    if (filterList.Count > 0)
    {
      filters = filterList.ToArray();
    }
    return status;
  }

  private HttpStatusCode ReadHeader()
  {
    HttpStatusCode status = HttpStatusCode.OK;
    FileStream resource = null;

    try
    {
      if (status == HttpStatusCode.OK)
      {
        resource = new FileStream(_restdFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        resource.Seek(0L, SeekOrigin.Begin);

        // Check for byte order mark
        byte[] bom = new byte[3];
        if (!(resource.Read(bom, 0, 3) == 3 && bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf))
        {
          _byteOrderMarkSize = 0;
        }

        resource.Seek(0L, SeekOrigin.Begin);

        char[] headerBuffer = new char[64];
        StreamReader resourceReader = new StreamReader(resource);
        resourceReader.Read(headerBuffer, 0, 64);

        string restdHeader = new String(headerBuffer);
        Regex recordLengthRegEx = new Regex(@"""blockSize"":(?<len>[0-9]+)");
        Match recordMatch = recordLengthRegEx.Match(restdHeader);

        if (!(recordMatch.Groups["len"].Success && Int32.TryParse(recordMatch.Groups["len"].Captures[0].Value, out _blockSize)))
        {
          status = HttpStatusCode.NotImplemented;
        }

        if (status == HttpStatusCode.OK && _blockSize < 4)
        {
          status = HttpStatusCode.NotImplemented;
        }
      }
    }
    catch (Exception)
    {
      status = HttpStatusCode.InternalServerError;
    }
    finally
    {
      if (resource != null)
      {
        resource.Close();
      }
    }

    return status;
  }

  private void WriteEntryProperties(char[] entryBuffer, TextWriter output)
  {
    int objectCount = 1;
    for (int i = 1; i < _blockSize; i++)
    {
      switch (entryBuffer[i])
      {
        case '{':
          objectCount++;
          break;

        case '}':
          objectCount--;
          break;
      }

      if (objectCount <= 0)
      {
        break;
      }
      else
      {
        output.Write(entryBuffer[i]);
      }
    }
  }
}

