<%@ WebHandler Language="C#" Class="PortalData" %>

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;

using DTrace = System.Diagnostics.Trace;

public class PortalData : IHttpHandler
{
  private static object SyncRoot = new object();

  public void ProcessRequest(HttpContext context)
  {
    context.Response.ContentType = "application/json";

#if DEBUG
    if (System.Diagnostics.Debugger.IsAttached)
    {
      context.Response.CacheControl = "no-cache";
    }
#endif

    try
    {
      //DTrace.WriteLine("PortalData: " + queryString);

      string[] resources = GetResources(context);

      string resource;
      int? key;

      if (!TryParseResourceAndKey(context.Request.QueryString["/"], out resource, out key))
      {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
      }
      else if (!(resource == "/" || resources.Count(r => r == resource) > 0))
      {
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
      }
      else
      {
        Restd providerData = new Restd(context.Server.MapPath("~/App_Data" + resource + ".restd"), context.Request.Url.AbsoluteUri.Replace(context.Request.Url.Query, "?/="));
        switch (context.Request.HttpMethod)
        {
          case "GET":
            if (key != null)
            {
              context.Response.StatusCode = (int)providerData.GetItem(key.Value, context.Response.Output);
            }
            else
            {
              context.Response.StatusCode = (int)providerData.Query(context.Request.QueryString["q"], context.Response.Output);
            }
            break;

          case "POST":
            if (resource == "/" || key.HasValue)
            {
              context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            else
            {
              context.Response.StatusCode = (int)providerData.PostItem(context.Request.Form[0], out key);
              if (context.Response.StatusCode == (int)HttpStatusCode.OK)
              {
                context.Response.Write(key);
              }
            }
            break;

          case "PUT":
            if (resource == "/" || !key.HasValue)
            {
              context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            else
            {
              context.Response.StatusCode = (int)providerData.PutItem(key.Value, context.Request.Form[0]);
              if (context.Response.StatusCode == (int)HttpStatusCode.OK)
              {
                context.Response.Write(key);
              }
            }
            break;

          case "DELETE":
            if (resource == "/" || !key.HasValue)
            {
              context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            else
            {
              context.Response.StatusCode = (int)providerData.DeleteItem(key.Value);
              if (context.Response.StatusCode == (int)HttpStatusCode.OK)
              {
                context.Response.Write(key);
              }
            }
            break;

          default:
            context.Response.StatusCode = (int)HttpStatusCode.NotImplemented;
            break;
        }
      }
    }
    catch (Exception ex)
    {
      DTrace.WriteLine(ex.Message);
      context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
    }
  }

  private string[] GetResources(HttpContext context)
  {
    string[] resources = Directory.GetFiles(context.Server.MapPath("~/App_Data/"), "*.restd");
    for (int i = 0; i < resources.Length; i++)
    {
      resources[i] = "/" + Path.GetFileNameWithoutExtension(resources[i]);
    }
    return resources;
  }

  private bool TryParseResourceAndKey(string query, out string resource, out int? key)
  {
    bool ok = true;

    if (!String.IsNullOrEmpty(query) && query[0] != '/')
    {
      ok = false;
    }

    string[] resourceParts = query.Substring(1).Split('/');

    resource = null;
    if (ok)
    {
      if (resourceParts.Length > 0)
      {
        resource = "/" + resourceParts[0];
      }
      else
      {
        ok = false;
      }
    }

    key = null;
    if (ok)
    {      
      int keyValue;
      if (resourceParts.Length > 1 && Int32.TryParse(resourceParts[1], out keyValue))
      {
        key = keyValue;
      }
    }

    return ok;
  }

  public bool IsReusable
  {
    get
    {
      return false;
    }
  }
}

