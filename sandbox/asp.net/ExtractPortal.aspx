<%@ Page Language="C#" %>

<!DOCTYPE html>

<script runat="server">

</script>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
  <title>Extract Portal</title>
</head>
<body>
  <form id="ExtractPortalForm" runat="server">
    <div>
      <h1>
        Extract Portal</h1>
      <p>
        Extract Provider Portal data into a restd file.</p>
      <div id="output">
      </div>
    </div>
  </form>

  <script src="http://ajax.microsoft.com/ajax/jquery/jquery-1.4.2.js" type="text/javascript"></script>

  <script src="json2.js" type="text/javascript"></script>

  <script type="text/javascript">
    $(function() {
      $.ajax({
        type: "GET",
        url: "OklahomaBroadbandDAta.svc/ProviderPortalDetails",
        dataType: "json",
        success: function(data, statusText) {
          $.each(data.d, function(index) {
            delete this.__metadata;
            delete this.OBJECTID;
            var postData = JSON.stringify(this);
            setTimeout(function() {
              $.ajax({
                type: "PUT",
                url: "PortalData.ashx?/=/ProviderPortalDetails(" + index + ")",
                dataType: "json",
                data: postData,
                success: function(result, statusText) {
                  $("#output").html($("#output").html() + result + "<br/>");
                },
                error: function(request, statusText, errorThrown) {
                  $("#output").html($("#output").html() + request.status + " - " + request.statusText + "<br/>" + statusText);
                }
              });
            }, 500);
          });
        },
        error: function(request, statusText, errorThrown) {
        }
      });
    });
  </script>

</body>
</html>
