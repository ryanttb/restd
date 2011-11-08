<%@ WebHandler Language="C#" Class="Tsv2Restd" %>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;

public class Tsv2Restd : IHttpHandler
{
  public void ProcessRequest(HttpContext context)
  {
    HttpStatusCode status = HttpStatusCode.OK;

    try
    {
      int blockSize = -1;
      if (context.Request.Files.Count == 0 || String.IsNullOrEmpty(context.Request.Form["blockSize"]) || !Int32.TryParse(context.Request.Form["blockSize"], out blockSize))
      {
        status = HttpStatusCode.BadRequest;
      }

      StreamReader inputReader = null;
      if (status == HttpStatusCode.OK)
      {
        inputReader = new StreamReader(context.Request.Files[0].InputStream);
      }

      string[] properties = null;
      if (status == HttpStatusCode.OK)
      {
        string header = inputReader.ReadLine();
        properties = header.Split('\t');

        if (properties.Length == 0)
        {
          status = HttpStatusCode.BadRequest;
        }
      }

      if (status == HttpStatusCode.OK)
      {
        context.Response.ContentType = "text/plain";
        context.Response.ContentEncoding = System.Text.Encoding.UTF8;
        context.Response.AddHeader("Content-Disposition", "attachment; filename=" + Path.ChangeExtension(context.Request.Files[0].FileName, "restd"));

        StreamWriter restdWriter = new StreamWriter(context.Response.OutputStream);

        string restdHeader = @"{""blockSize"":" + blockSize + ",data:[";

        restdWriter.Write(restdHeader + new String(' ', 512 - restdHeader.Length));
        while (status == HttpStatusCode.OK && !inputReader.EndOfStream)
        {
          string tsvLine = inputReader.ReadLine();

          List<string> values = new List<string>(tsvLine.Split('\t'));

          while (status == HttpStatusCode.OK && values.Count < properties.Length)
          {
            string moreLines = inputReader.ReadLine();
            if (moreLines == null)
            {
              status = HttpStatusCode.BadRequest;
            }
            
            string[] moreValues = null;
            if (status == HttpStatusCode.OK)
            {
              moreValues = moreLines.Split('\t');
              if (moreValues.Length == 0)
              {
                status = HttpStatusCode.BadRequest;
              }
            }

            if (status == HttpStatusCode.OK)
            {
              values[values.Count - 1] += ("\n" + moreValues[0]);
              values.AddRange(moreValues.Skip(1));
            }
          }
          
          if (status == HttpStatusCode.OK && values.Count != properties.Length)
          {
            status = HttpStatusCode.BadRequest;
          }

          string restdEntity = "";
          if (status == HttpStatusCode.OK)
          {
            restdEntity += "{";
            
            for (int i = 0; i < properties.Length; i++)
            {
              if (i > 0)
              {
                restdEntity += ",";
              }
              
              restdEntity += String.Format(@"""{0}"":{1}", properties[i], values[i]);
            }

            restdEntity += "},";

            if (restdEntity.Length > blockSize)
            {
              status = HttpStatusCode.NotImplemented;
            }
          }

          if (status == HttpStatusCode.OK)
          {
            restdWriter.Write(restdEntity + new String(' ', blockSize - restdEntity.Length));
          }          
        }

        if (status == HttpStatusCode.OK)
        {
          restdWriter.Write("null]}");
        }
        
        restdWriter.Flush();
      }
    }
    catch (Exception)
    {
      status = HttpStatusCode.InternalServerError;
    }
    finally
    {
      context.Response.StatusCode = (int)status;
    }
  }

  public bool IsReusable
  {
    get
    {
      return false;
    }
  }

}