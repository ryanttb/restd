<%@ WebHandler Language="C#" Class="RestdSvc" %>

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;

using DTrace = System.Diagnostics.Trace;

public class RestdSvc : IHttpHandler
{
  private static object SyncRoot = new object();

  /// <summary>
  /// Set the options for this service
  /// </summary>
  /// <returns>New options</returns>
  /// <remarks>
  /// You can modify the return value directly or
  /// inherit RestdSvc in another handler and override this method.
  /// </remarks>
  protected virtual RestdOptions GetOptions()
  {
    return new RestdOptions()
    {
      GetRestdFile = (resourceName) => HttpContext.Current.Server.MapPath("~/App_Data" + resourceName + ".restd"),
      GetBaseUri = () => HttpContext.Current.Request.Url.AbsoluteUri.Replace(HttpContext.Current.Request.Url.Query, "?/="),
      CanGet = null,
      CanGetItem = null,
      CanPost = null,
      CanPut = null,
      CanDelete = null
    };
  }

  public void ProcessRequest(HttpContext context)
  {
    context.Response.ContentType = "application/json";

#if DEBUG
    if (System.Diagnostics.Debugger.IsAttached)
    {
      context.Response.CacheControl = "no-cache";
    }
#endif

    HttpStatusCode status = HttpStatusCode.OK;

    try
    {
      DTrace.WriteLine("RestdSvc: " + context.Request.QueryString.ToString());

      string[] resources = GetResources(context);

      string resource;
      int? key;

      if (!TryParseResourceAndKey(context.Request.QueryString["/"], out resource, out key))
      {
        status = HttpStatusCode.BadRequest;
      }
      else if (!(resource == "/" || resources.Count(r => r == resource) > 0))
      {
        status = HttpStatusCode.NotFound;
      }
      else
      {
        string contentType = "";
        string content = "";

        Restd restd = new Restd(resource, GetOptions());
        switch (context.Request.HttpMethod)
        {
          case "GET":
            if (key != null)
            {
              status = restd.GetItem(key.Value, context.Response.Output);
            }
            else
            {
              status = restd.Query(context.Request.QueryString["q"], context.Response.Output);
            }
            break;

          case "POST":
            if (resource == "/" || key.HasValue)
            {
              status = HttpStatusCode.BadRequest;
            }

            if (status == HttpStatusCode.OK)
            {
              string[] contentTypeParts = (context.Request.ContentType != null ? context.Request.ContentType.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries) : null);
              if (contentTypeParts == null || contentTypeParts.Length == 0)
              {
                status = HttpStatusCode.BadRequest;
              }
              else
              {
                contentType = contentTypeParts[0];
              }
            }

            if (status == HttpStatusCode.OK)
            {
              switch (contentType)
              {
                case "application/json":
                  byte[] contentBytes = new byte[context.Request.ContentLength];
                  context.Request.InputStream.Read(contentBytes, 0, context.Request.ContentLength);
                  content = System.Text.UTF8Encoding.UTF8.GetString(contentBytes);
                  break;

                default:
                  if (context.Request.Form.Count > 0)
                  {
                    content += "{";

                    for (int i = 0; i < context.Request.Form.Count; i++)
                    {
                      if (i > 0)
                      {
                        content += ",";
                      }

                      content += String.Format(@"""{0}"":""{1}""", context.Request.Form.Keys[i], context.Request.Form[i]);
                    }

                    content += "}";
                  }
                  break;
              }
            }

            if (String.IsNullOrEmpty(content))
            {
              status = HttpStatusCode.BadRequest;
            }
            else
            {
              status = restd.PostItem(content, out key);
            }

            if (status == HttpStatusCode.OK)
            {
              context.Response.Write(key);
            }
            break;

          case "PUT":
            if (resource == "/" || !key.HasValue)
            {
              status = HttpStatusCode.BadRequest;
            }

            if (status == HttpStatusCode.OK)
            {
              string[] contentTypeParts = (context.Request.ContentType != null ? context.Request.ContentType.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries) : null);
              if (contentTypeParts == null || contentTypeParts.Length == 0)
              {
                status = HttpStatusCode.BadRequest;
              }
              else
              {
                contentType = contentTypeParts[0];
              }
            }

            if (status == HttpStatusCode.OK)
            {
              switch (contentType)
              {
                case "application/json":
                  byte[] contentBytes = new byte[context.Request.ContentLength];
                  context.Request.InputStream.Read(contentBytes, 0, context.Request.ContentLength);
                  content = System.Text.UTF8Encoding.UTF8.GetString(contentBytes);
                  break;

                default:
                  if (context.Request.Form.Count > 0)
                  {
                    content += "{";

                    for (int i = 0; i < context.Request.Form.Count; i++)
                    {
                      if (i > 0)
                      {
                        content += ",";
                      }

                      content += String.Format(@"""{0}"":""{1}""", context.Request.Form.Keys[i], context.Request.Form[i]);
                    }

                    content += "}";
                  }
                  break;
              }

              if (String.IsNullOrEmpty(content))
              {
                status = HttpStatusCode.BadRequest;
              }
              else
              {
                status = restd.PutItem(key.Value, content);
              }

              if (status == HttpStatusCode.OK)
              {
                context.Response.Write(key);
              }
            }
            break;

          case "DELETE":
            if (resource == "/" || !key.HasValue)
            {
              status = HttpStatusCode.BadRequest;
            }
            else
            {
              status = restd.DeleteItem(key.Value);
              if (status == HttpStatusCode.OK)
              {
                context.Response.Write(key);
              }
            }
            break;

          default:
            status = HttpStatusCode.NotImplemented;
            break;
        }
      }
    }
    catch (Exception ex)
    {
      DTrace.WriteLine(ex.Message);
      status = HttpStatusCode.InternalServerError;
    }
    finally
    {
      context.Response.StatusCode = (int)status;
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


