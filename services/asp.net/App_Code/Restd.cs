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
/// Map a restd resource to a physical restd file
/// </summary>
/// <param name="resourceName">Restd resource name starting with /</param>
/// <returns>Path to a physical file</returns>
/// <remarks>
/// This is the only required delegate in the RestdOptions.
/// </remarks>
public delegate string GetRestdFileDelegate(string resourceName);

/// <summary>
/// Return a URI to the restd resources so that a restd resource name can be appended
/// </summary>
/// <returns>Uri to resource base</returns>
/// <remarks>
/// A URI so that the return value + /resourcename makes sense for your service.
/// </remarks>
public delegate string GetBaseUriDelegate();

/// <summary>
/// Allow/deny a client to query a resource
/// </summary>
/// <param name="resourceName">Restd resource to query</param>
/// <param name="query">Query, empty if not supplied</param>
/// <returns>true to allow the client to query</returns>
/// <remarks>
/// You may modify the query. The modified query will be used instead.
/// </remarks>
public delegate bool CanGetDelegate(string resourceName, ref string query);

/// <summary>
/// While a GET is taking place, allow/deny a client to a specific item
/// </summary>
/// <param name="resourceName">Restd resource to query</param>
/// <param name="key">Restd key of the item being considered</param>
/// <param name="content">Content of the item being considered</param>
/// <returns>true to allow the client to retrieve this item</returns>
/// <remarks>
/// You may modify the content. The modified JSON will be sent instead.
/// Do not modify the _restd property.
/// </remarks>
public delegate bool CanGetItemDelegate(string resourceName, int key, ref string content);

/// <summary>
/// Allow/deny a client to insert a specific item
/// </summary>
/// <param name="resourceName">Restd resource to insert into</param>
/// <param name="content">Content of the item being inserted</param>
/// <returns>true to allow the client to insert this item</returns>
/// <remarks>
/// You may modify the content. The modified JSON will be inserted instead.
/// </remarks>
public delegate bool CanPostDelegate(string resourceName, ref string content);

/// <summary>
/// Allow/deny a client to update a specific item
/// </summary>
/// <param name="resourceName">Restd resource to update into</param>
/// <param name="key">Restd key of the item being updated</param>
/// <param name="content">Content of the item being updated</param>
/// <returns>true to allow the client to update this item</returns>
/// <remarks>
/// You may modify the content. The modified JSON will be updated instead.
/// </remarks>
public delegate bool CanPutDelegate(string resourceName, int key, ref string content);

/// <summary>
/// Allow/deny a client to delete a specific item
/// </summary>
/// <param name="resourceName">Restd resource to delete from</param>
/// <param name="key">Restd key of the item being deleted</param>
/// <param name="content">Content of the item being deleted</param>
/// <returns>true to allow the client to delete this item</returns>
/// <remarks>
/// You cannot modify the content of the item being deleted.
/// </remarks>
public delegate bool CanDeleteDelegate(string resourceName, int key, string content);

public class RestdOptions
{
  public GetRestdFileDelegate GetRestdFile = null;
  public GetBaseUriDelegate GetBaseUri = null;

  public CanGetDelegate CanGet = null;
  public CanGetItemDelegate CanGetItem = null;
  public CanPostDelegate CanPost = null;
  public CanPutDelegate CanPut = null;
  public CanDeleteDelegate CanDelete = null;
}

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

  private RestdOptions _options = null;

  private string _restdFile = "";
  private string _serviceUri = "";
  private string _resourceName = "";
  private int _byteOrderMarkSize = 3;
  private int _headerSize = 64;
  private int _blockSize = -1;

  private HttpStatusCode _constructorStatus = HttpStatusCode.OK;

  public Restd(string resourceName, RestdOptions options)
  {
    HttpStatusCode status = HttpStatusCode.OK;

    if (String.IsNullOrEmpty(resourceName) || options == null || options.GetRestdFile == null)
    {
      status = HttpStatusCode.BadRequest;
    }
    else
    {
      _resourceName = resourceName;
      _options = options;
      _restdFile = _options.GetRestdFile(resourceName);

      if (_options.GetBaseUri != null)
      {
        _serviceUri = _options.GetBaseUri();
      }

      if (!File.Exists(_restdFile))
      {
        status = HttpStatusCode.NotFound;
      }
    }

    if (status == HttpStatusCode.OK)
    {
      status = ReadHeader();
    }

    _constructorStatus = status;
  }

  /// <summary>
  /// Return an array of entires as a string
  /// </summary>
  public string Query(string query)
  {
    StringBuilder entryBuilder = new StringBuilder();
    StringWriter entryWriter = null;

    try
    {
      entryWriter = new StringWriter(entryBuilder);

      if (Query(query, entryWriter) == HttpStatusCode.OK)
      {
        return entryBuilder.ToString();
      }
      else
      {
        return "[]";
      }
    }
    catch (Exception)
    {
      return "[]";
    }
    finally
    {
      entryWriter.Close();
    }
  }

  /// <summary>
  /// Retrieve an array of entires and write them to an output stream
  /// </summary>
  public HttpStatusCode Query(string query, TextWriter output)
  {
    HttpStatusCode status = _constructorStatus;

    if (status != HttpStatusCode.OK)
    {
      return status;
    }

    FileStream resourceStream = null;

    try
    {
      query = query ?? "";
      if (_options.CanGet != null && !_options.CanGet(this._restdFile, ref query))
      {
        status = HttpStatusCode.Unauthorized;
      }

      string[] filters = null;
      if (status == HttpStatusCode.OK)
      {
        status = ParseQuery(query, out filters);
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
        resourceStream.Seek(_headerSize + _byteOrderMarkSize, SeekOrigin.Begin);
        StreamReader resourceReader = new StreamReader(resourceStream);

        entryBuffer = new char[_blockSize];

        output.Write("[");

        int index = 0, total = 0;
        int bytesRead = resourceReader.Read(entryBuffer, 0, _blockSize);
        while (bytesRead == _blockSize)
        {
          if (entryBuffer[0] == '{')
          {
            string entryString = null;
            bool canGetItem = true;

            if (_options.CanGetItem != null)
            {
              if (entryString == null)
              {
                entryString = GetEntryString(index, entryBuffer);
              }

              canGetItem = _options.CanGetItem(_restdFile, index, ref entryString);
            }

            if (canGetItem)
            {
              if (filters == null)
              {
                if (total > 0)
                {
                  output.Write(',');
                }

                if (entryString != null)
                {
                  output.Write(entryString);
                }
                else
                {
                  output.Write(EntryPrefixFormat, _resourceName, index);

                  WriteEntryProperties(entryBuffer, output);

                  output.Write('}');
                }

                total++;
              }
              else
              {
                if (entryString == null)
                {
                  entryString = GetEntryString(index, entryBuffer);
                }

                if (IsInFilter(entryString, filters))
                {
                  if (total > 0)
                  {
                    output.Write(",");
                  }

                  output.Write(entryString);

                  total++;
                }
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
        long entityCount = (resourceInfo.Length - _headerSize) / _blockSize;
        if (key >= entityCount)
        {
          status = HttpStatusCode.NotFound;
        }
      }

      char[] entryBuffer = null;

      if (status == HttpStatusCode.OK)
      {
        entryBuffer = new char[_blockSize];
        resourceStream.Seek(_headerSize + _byteOrderMarkSize + key * _blockSize, SeekOrigin.Begin);

        StreamReader resourceReader = new StreamReader(resourceStream);
        int bytesRead = resourceReader.Read(entryBuffer, 0, _blockSize);
        if (!(bytesRead == _blockSize && entryBuffer[0] == '{'))
        {
          status = HttpStatusCode.NotFound;
        }
      }

      if (status == HttpStatusCode.OK)
      {
        output.Write(EntryPrefixFormat, _resourceName, key);

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

  public HttpStatusCode PostItem(string entityData, out int? key, TextWriter output)
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
        if (_options.CanPost != null && !_options.CanPost(_restdFile, ref entityData))
        {
          status = HttpStatusCode.Unauthorized;
        }
      }

      if (status == HttpStatusCode.OK)
      {
        lock (SyncRoot)
        {
          FileInfo resourceInfo = new FileInfo(_restdFile);
          long entityCount = (resourceInfo.Length - _headerSize) / _blockSize;
          long insertOffset = _headerSize + _byteOrderMarkSize + entityCount * _blockSize;

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

    if (status == HttpStatusCode.OK)
    {
      status = GetItem(key.Value, output);
    }

    return status;
  }

  public HttpStatusCode PutItem(int key, string entityData, TextWriter output)
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
        if (_options.CanPut != null && !_options.CanPut(_restdFile, key, ref entityData))
        {
          status = HttpStatusCode.Unauthorized;
        }
      }

      if (status == HttpStatusCode.OK)
      {
        lock (SyncRoot)
        {
          FileInfo resourceInfo = new FileInfo(_restdFile);
          long entityCount = (resourceInfo.Length - _headerSize) / _blockSize;

          if (key >= entityCount)
          {
            status = HttpStatusCode.NotFound;
          }
          else
          {
            long insertOffset = _headerSize + _byteOrderMarkSize + key * _blockSize;

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

    if (status == HttpStatusCode.OK)
    {
      status = GetItem(key, output);
    }

    return status;
  }

  public HttpStatusCode DeleteItem(int key, TextWriter output)
  {
    HttpStatusCode status = _constructorStatus;

    FileStream resourceStream = null;
    string entryString = "";

    try
    {
      StringBuilder entryBuilder = new StringBuilder();
      StringWriter entryWriter = new StringWriter(entryBuilder);

      if (status == HttpStatusCode.OK)
      {
        status = GetItem(key, entryWriter);
      }

      if (status == HttpStatusCode.OK)
      {
        entryString = entryWriter.ToString();
      }

      if (status == HttpStatusCode.OK && _options.CanDelete != null && !_options.CanDelete(_restdFile, key, entryString))
      {
        status = HttpStatusCode.Unauthorized;
      }

      if (status == HttpStatusCode.OK)
      {
        lock (SyncRoot)
        {
          FileInfo resourceInfo = new FileInfo(_restdFile);
          long entityCount = (resourceInfo.Length - _headerSize) / _blockSize;

          if (key < entityCount)
          {
            long insertOffset = _headerSize + _byteOrderMarkSize + key * _blockSize;

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
    }
    catch (Exception ex)
    {
      Trace.WriteLine(ex.Message);
      status = HttpStatusCode.InternalServerError;
    }

    if (status == HttpStatusCode.OK)
    {
      output.Write(entryString);
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

        char[] headerBuffer = new char[_headerSize];
        StreamReader resourceReader = new StreamReader(resource);
        resourceReader.Read(headerBuffer, 0, _headerSize);

        string restdHeader = new String(headerBuffer);

        Regex headerSizeRegEx = new Regex(@"""headerSize"":(?<len>[0-9]+)");
        Match headerSizeMatch = headerSizeRegEx.Match(restdHeader);

        if (headerSizeMatch.Groups["len"].Success && Int32.TryParse(headerSizeMatch.Groups["len"].Captures[0].Value, out _headerSize))
        {
          // New headerSize
          resource.Seek(0L, SeekOrigin.Begin);

          headerBuffer = new char[_headerSize];
          resourceReader.Read(headerBuffer, 0, _headerSize);
        }

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

  private string GetEntryString(int index, char[] entryBuffer)
  {
    StringBuilder entryBuilder = new StringBuilder();
    StringWriter entryWriter = new StringWriter(entryBuilder);

    entryWriter.Write(EntryPrefixFormat, _resourceName, index);
    WriteEntryProperties(entryBuffer, entryWriter);
    entryWriter.Write('}');

    entryWriter.Close();
    return entryBuilder.ToString();
  }
}

