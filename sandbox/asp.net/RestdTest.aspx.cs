using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using DTrace = System.Diagnostics.Trace;

public partial class Admin_RestdTest : System.Web.UI.Page
{
  protected void Page_Load(object sender, EventArgs e)
  {
    try
    {
      StreamReader sr = new StreamReader(Server.MapPath("~/App_Data/ProviderMarkup.restd"));
      char[] restdHeader = new char[512];
      sr.Read(restdHeader, 0, 512);
      sr.Close();

      string s = new String(restdHeader);
      Regex recordLengthRegEx = new Regex(@"""blockSize"":(?<len>[0-9]+)");
      Match recordMatch = recordLengthRegEx.Match(s);
      int blockSize;
      if (Int32.TryParse(recordMatch.Groups["len"].Captures[0].Value, out blockSize))
      {
        DTrace.WriteLine("ProviderMarkup blockSize: " + blockSize);
      }
      else
      {
        DTrace.WriteLine("ProviderMarkup header is not valid");
      }
    }
    catch (Exception ex)
    {
      DTrace.WriteLine(ex.Message);
    }
  }
}
