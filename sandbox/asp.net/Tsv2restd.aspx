<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Tsv2restd.aspx.cs" Inherits="Tsv2Restd" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
  <title>Tab-separated value to restd</title>
</head>
<body>
  <form id="Tsv2restdForm" runat="server">
    <div>
      <h1>
        Tab-separated value to restd</h1>
      <p>
        Use this page to convert a tab-separated value text file (having field names on the first line) into a sized restd file.</p>
      <p>
        If a row is too long for the block size given, this page will not convert the file and return an error instead.</p>
      <p>
        Even though the response is JSON, the MIME type returned is text/plain so browsers will download it correctly.</p>
      <table>
        <tr>
          <th>
            <asp:Label runat="server" AssociatedControlID="fileInput">File</asp:Label>
          </th>
          <td>
            <asp:FileUpload ID="fileInput" runat="server" />
          </td>
        </tr>
        <tr>
          <th>
            <asp:Label runat="server" AssociatedControlID="blockSize">Block Size</asp:Label>
          </th>
          <td>
            <asp:TextBox ID="blockSize" runat="server"></asp:TextBox>
          </td>
        </tr>
      </table>
      <asp:Button ID="cmdConvert" runat="server" UseSubmitBehavior="true" Text="Convert" PostBackUrl="~/Tsv2Restd.ashx" />
      <span>
        <asp:Literal ID="litStatus" runat="server"></asp:Literal></span>
    </div>
  </form>
</body>
</html>
