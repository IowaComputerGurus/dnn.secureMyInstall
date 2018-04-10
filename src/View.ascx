<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="View.ascx.cs" Inherits="ICG.Modules.SecureMyInstall.View" %>

<h2>Instructions (MUST READ!)</h2>

<p>
    <strong>YOU MUST FOLLOW THESE INSTRUCTIONS DIRECTLY.</strong>  Failure to follow these will result in your system being unavailable.
</p>

<h3>Pre-Completion Steps</h3>

<p>Before starting this operation, create a backup of your DotNetNuke system database as well as the current web.config, should an error occur while working within the installation.
both wll receive extensive updates as this process completes.</p>

<p>Also, ensure that you have the site setup for "Public" or "Verified" registration, as it will be necessary for you to create a new user account.</p>

<h3>Completion Process</h3>

<ol>
    <li>Create a new page, which refresh interval of 60 seconds and provide view access to registered users.  Hide from the menu and remember the URL for security purposes.</li>
    <li>Add an instance of the "Secure My Install" module to this newly created page, ensure that "Registered Users" can view.</li>
    <li>CLick the button below to complete "Phase 1".  This will trigger a DotNetNuke scheduled job to start the process.  Once you see that the machine key values have updated proceed to the next step.</li>
    <li>Register as a New user to the site and return to the page which was created in #2</li>
    <li>Click button below to promote the current user to a Super User</li>
    <li>Remove view access from registerd users to this page, as you have now gotten into the updated installation.</li>
    <li>Click the button to complete "Phase 2"</li>
    <li>Once the scheduled job has completed, validate that your existing accounts are working, and login to your portal with your existing account and remove the temp user.</li>
</ol>    

<p><strong>NOTE: Each of these processes can take a period of time to successfully execute, be sure to let the process complete. Once phase 1 has been completed DO NOT complete it again.</strong></p>

<h2>Configuration Information:</h2>

<p><strong>Validation Key:</strong> <asp:Label ID="lblValidationKey" runat="server" /></p>
<p><strong>Decryption Key:</strong> <asp:Label ID="lblDecryptionKey" runat="server" /></p>

<p>
    <asp:LinkButton ID="btnPhase1" runat="server" OnClientClick="return confirm('By clicking Ok I agree to accept any risks associated with running this process');"
        Text="Click Here to Start Phase 1" onclick="btnPhase1_Click" />
</p>

<p>
    <asp:LinkButton ID="btnPromote" runat="server" 
        Text="Click Here to Promote Current User To Super User" 
        onclick="btnPromote_Click" />
</p>

<p>
    <asp:LinkButton ID="btnPhase2" runat="server" 
        Text="Click Here To Start Phase 2" onclick="btnPhase2_Click" />
</p>