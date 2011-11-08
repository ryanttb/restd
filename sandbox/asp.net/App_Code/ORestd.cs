using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Reads and writes JSON data to a single restd resource file
/// </summary>
/// <remarks>
/// Maintains header and index information.
/// Calculates indexes (if any) during construction.
/// </remarks>
public class ORestd
{
  private static object SyncRoot = new object();

  private string EntryPrefixFormat = @"{{ ""__metadata"": {{ ""uri"": ""{0}{1}({2})"", ""type"": ""object""}}, ";

  private string _restdFile = "";
  private string _serviceUri = "";
  private string _resourcePath = "";
  private int _byteOrderMarkSize = 3;
  private int _blockSize = -1;
  private HttpStatusCode _constructorStatus = HttpStatusCode.OK;

  public ORestd(string restdFile, string serviceUri)
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
  public HttpStatusCode Query(bool count, string query, TextWriter output)
  {
    HttpStatusCode status = _constructorStatus;

    FileStream resourceStream = null;

    try
    {
      string[] filter = null, select = null;
      if (status == HttpStatusCode.OK)
      {
        status = ParseQuery(query, out filter, out select);
      }

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

        if (count && (filter == null || filter.Length == 0))
        {
          FileInfo resourceInfo = new FileInfo(_restdFile);
          long entityCount = (resourceInfo.Length - 64) / _blockSize;
          output.Write(entityCount);
        }
        else
        {
          entryBuffer = new char[_blockSize];

          if (!count)
          {
            output.Write(@"{ ""d"": [");
          }

          int index = 0, total = 0;
          int bytesRead = resourceReader.Read(entryBuffer, 0, _blockSize);
          while (bytesRead == _blockSize)
          {
            if (entryBuffer[0] == '{')
            {
              if (filter == null || filter.Length == 0)
              {
                if (total > 0)
                {
                  output.Write(',');
                }

                output.Write(EntryPrefixFormat, _serviceUri, _resourcePath, index);

                WriteEntryProperties(entryBuffer, select, output);

                output.Write('}');

                total++;
              }
              else
              {
                StringBuilder entryBuilder = new StringBuilder();
                StringWriter entryWriter = new StringWriter(entryBuilder);

                entryWriter.Write(EntryPrefixFormat, _serviceUri, _resourcePath, index);
                WriteEntryProperties(entryBuffer, select, entryWriter);
                entryWriter.Write('}');
                entryWriter.Close();
                string entry = entryBuilder.ToString();

                entryBuilder = new StringBuilder();
                entryWriter = new StringWriter(entryBuilder);

                entryWriter.Write(EntryPrefixFormat, _serviceUri, _resourcePath, index);
                WriteEntryProperties(entryBuffer, null, entryWriter);
                entryWriter.Write('}');
                entryWriter.Close();
                string fullEntry = entryBuilder.ToString();

                bool inFilter = true;
                for (int i = 0; i < filter.Length; i++)
                {
                  if (!IsInFilter(fullEntry, filter[i]))
                  {
                    inFilter = false;
                    break;
                  }
                }

                if (inFilter)
                {
                  if (!count)
                  {
                    if (total > 0)
                    {
                      output.Write(",");
                    }

                    output.Write(entry);
                  }

                  total++;
                }
              }
            }

            index++;
            bytesRead = resourceReader.Read(entryBuffer, 0, _blockSize);
          }

          if (count)
          {
            output.Write(total);
          }
          else
          {
            output.Write("] }");
          }
        }
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
  public HttpStatusCode GetItem(int key, string query, TextWriter output)
  {
    HttpStatusCode status = _constructorStatus;

    FileStream resourceStream = null;

    try
    {
      string[] filter = null, select = null;
      if (status == HttpStatusCode.OK)
      {
        status = ParseQuery(query, out filter, out select);
      }

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
        output.Write(@"{ ""d"": [");
        output.Write(EntryPrefixFormat, _serviceUri, _resourcePath, key);

        WriteEntryProperties(entryBuffer, select, output);

        output.Write("} ] }");
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

  private bool IsInFilter(string entry, string filter)
  {
    bool ok = true;

    Regex entryEq = new Regex(filter);
    ok = entryEq.Match(entry).Success;

    return ok;
  }

  private HttpStatusCode ParseQuery(string query, out string[] filter, out string[] select)
  {
    HttpStatusCode status = HttpStatusCode.OK;

    filter = select = null;
    string queryParamsString = null;

    int paramsIndex = query.IndexOf('?');
    if (paramsIndex > 0)
    {
      queryParamsString = query.Substring(paramsIndex + 1);
    }
    else
    {
      queryParamsString = query;
    }

    string[] queryParams = queryParamsString.Split('&');
    foreach (string param in queryParams)
    {
      if (param.StartsWith("$filter="))
      {
        string[] filters = param.Substring(8).Split(new string[] {" and "}, StringSplitOptions.RemoveEmptyEntries);
        filter = new string[filters.Length];
        for (int i = 0; i < filters.Length; i++)
        {
          Regex eqNumber = new Regex(@"(?<property>\w+)\s+eq\s+(?<value>.*)?\s*");
          Match eqNumberMatch = eqNumber.Match(filters[i]);
          if (eqNumberMatch.Groups["property"].Success && eqNumberMatch.Groups["value"].Success)
          {
            filter[i] = String.Format(@"""{0}""\:\s*{1}\s*[,}}]", eqNumberMatch.Groups["property"].Captures[0].Value, eqNumberMatch.Groups["value"].Captures[0].Value);
          }

          Regex eqString = new Regex(@"(?<property>\w+)\s+eq\s+'(?<value>.*)?'\s*");
          Match eqStringMatch = eqString.Match(filters[i]);
          if (eqStringMatch.Groups["property"].Success && eqStringMatch.Groups["value"].Success)
          {
            filter[i] = String.Format(@"""{0}""\:\s*""{1}""\s*[,}}]", eqStringMatch.Groups["property"].Captures[0].Value, eqStringMatch.Groups["value"].Captures[0].Value);
          }

          Regex subStringOf = new Regex(@"substringof\s*\(\s*'(?<value>.*)?'\s*,\s*(?<property>\w+)\s*\)\s+eq\s+true\s*");
          Match subStringOfMatch = subStringOf.Match(filters[i]);
          if (subStringOfMatch.Groups["property"].Success && subStringOfMatch.Groups["value"].Success)
          {
            filter[i] = String.Format(@"""{0}""\:\s*""[^""]*{1}[^""]*""\s*[,}}]", subStringOfMatch.Groups["property"].Captures[0].Value, subStringOfMatch.Groups["value"].Captures[0].Value);
          }
        }
      }

      if (param.StartsWith("$select="))
      {
        select = param.Substring(8).Split(',');
      }
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

  private void WriteEntryProperties(char[] entryBuffer, string[] select, TextWriter output)
  {
    if (select == null)
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
    else
    {
      int propCount = 0;
      JObject entryJson = JObject.Parse(new String(entryBuffer).Trim());
      foreach (JProperty entryProperty in entryJson.Properties())
      {
        if (select.Contains(entryProperty.Name))
        {
          if (propCount > 0)
          {
            output.Write(',');
          }
          output.Write(entryProperty.ToString(Formatting.None));
          propCount++;
        }
      }
    }
  }
}
