<%@ WebHandler Language="C#" Class="GetJson" %>

using System;
using System.IO;
using System.Net;
using System.Web;

public class GetJson : IHttpHandler
{

  public void ProcessRequest(HttpContext context)
  {
    context.Response.ContentType = "application/json";
    HttpStatusCode status = HttpStatusCode.OK;

    try
    {
      if (context.Request.QueryString.Count == 0)
      {
        status = HttpStatusCode.BadRequest;
      }

      string jsonFile = "";

      if (status == HttpStatusCode.OK)
      {
        jsonFile = context.Server.MapPath("~/App_Data/" + context.Request.QueryString[0]);

        if (!File.Exists(jsonFile))
        {
          status = HttpStatusCode.NotFound;
        }
      }

      if (status == HttpStatusCode.OK)
      {
        context.Response.TransmitFile(jsonFile);
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