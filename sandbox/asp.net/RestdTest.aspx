<%@ Page Language="C#" AutoEventWireup="true" CodeFile="RestdTest.aspx.cs" Inherits="Admin_RestdTest" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
  <title>restd Test</title>
  <style type="text/css">
    .output
    {
      font-family: Consolas, Courier New;
    }
  </style>
</head>
<body>
  <form id="RestdTestForm" runat="server">
    <h1>
      restd Test</h1>
    <h2>
      GET</h2>
    <div id="pnlGet">
      <label>
        Query
        <input type="text" value="/EO_NATURAL_COMM" style="width: 512px;" /></label>
      <a href="javascript:void(0);">GET</a>
      <h3>
        output</h3>
      <div class="output">
      </div>
    </div>
    <h2>
      POST</h2>
    <div id="pnlPost">
      <label>
        Query
        <input type="text" value="/ProviderPortalDetails" /></label>
      <label>
        Body
        <textarea rows="4" cols="40"></textarea>
      </label>
      <a href="javascript:void(0);">POST</a>
      <h3>
        output</h3>
      <div class="output">
      </div>
    </div>
    <h2>
      PUT</h2>
    <div id="pnlPut">
      <label>
        Query
        <input type="text" value="/" /></label>
      <label>
        Body
        <textarea rows="4" cols="40"></textarea>
      </label>
      <a href="javascript:void(0);">PUT</a>
      <h3>
        output</h3>
      <div class="output">
      </div>
    </div>
    <h2>
      DELETE</h2>
    <div id="pnlDelete">
      <label>
        Query
        <input type="text" value="/" /></label>
      <a href="javascript:void(0);">DELETE</a>
      <h3>
        output</h3>
      <div class="output">
      </div>
    </div>
  </form>

  <script src="http://ajax.microsoft.com/ajax/jquery/jquery-1.4.2.js" type="text/javascript"></script>

  <script src="json2.js" type="text/javascript"></script>

  <script type="text/javascript">
    $(function() {
      $("#pnlGet a").click(function() {
        $.ajax({
          type: "GET",
          url: "Restd.ashx?/=" + $("#pnlGet input").val(),
          dataType: "json",
          success: function(data, statusText) {
            $("#pnlGet .output").html(JSON.stringify(data) + "<br/>" + statusText);
          },
          error: function(request, statusText, errorThrown) {
            $("#pnlGet .output").html(request.status + " - " + request.statusText + "<br/>" + statusText);
          }
        });
      });

      $("#pnlPost a").click(function() {
        $.ajax({
          type: "POST",
          url: "Restd.ashx?/=" + $("#pnlPost input").val(),
          dataType: "json",
          data: $("#pnlPost textarea").val(),
          success: function(data, statusText) {
            $("#pnlPost .output").html(JSON.stringify(data) + "<br/>" + statusText);
          },
          error: function(request, statusText, errorThrown) {
            $("#pnlPost .output").html(request.status + " - " + request.statusText + "<br/>" + statusText);
          }
        });
      });

      $("#pnlPut a").click(function() {
        $.ajax({
          type: "PUT",
          url: "Restd.ashx?/=" + $("#pnlPut input").val(),
          dataType: "json",
          data: $("#pnlPut textarea").val(),
          success: function(data, statusText) {
            $("#pnlPut .output").html(JSON.stringify(data) + "<br/>" + statusText);
          },
          error: function(request, statusText, errorThrown) {
            $("#pnlPut .output").html(request.status + " - " + request.statusText + "<br/>" + statusText);
          }
        });
      });

      $("#pnlDelete a").click(function() {
        $.ajax({
          type: "DELETE",
          url: "Restd.ashx?/=" + $("#pnlDelete input").val(),
          dataType: "json",
          data: $("#pnlDelete textarea").val(),
          success: function(data, statusText) {
            $("#pnlDelete .output").html(JSON.stringify(data) + "<br/>" + statusText);
          },
          error: function(request, statusText, errorThrown) {
            $("#pnlDelete .output").html(request.status + " - " + request.statusText + "<br/>" + statusText);
          }
        });
      });
    });
  </script>

</body>
</html>
