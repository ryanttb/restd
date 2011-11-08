using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Net;

public partial class RestdClassTest : System.Web.UI.Page
{
  protected void Page_Load(object sender, EventArgs e)
  {
    ORestd providerMarkup = new ORestd(Server.MapPath("~/App_Data/OK_Census_Blocks.restd"), "");

    StringBuilder outBuilder = new StringBuilder();
    StringWriter outWriter = new StringWriter(outBuilder);

    HttpStatusCode status = providerMarkup.Query(false, "$filter=blockId eq '400019766001060'", outWriter);

    outWriter.Close();

    litOutput.Text = status.ToString() + "<br/>" + outBuilder.ToString();
  }
}
