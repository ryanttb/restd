using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using DTrace = System.Diagnostics.Trace;

public partial class ORestdTest : System.Web.UI.Page
{
  protected void Page_Load(object sender, EventArgs e)
  {
    try
    {
    }
    catch (Exception ex)
    {
      DTrace.WriteLine(ex.Message);
    }
  }
}
