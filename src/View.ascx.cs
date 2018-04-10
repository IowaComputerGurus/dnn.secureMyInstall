using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using System.Web.UI;
using System.Web.UI.WebControls;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Data;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Modules.Actions;
using DotNetNuke.Services.Exceptions;
using DotNetNuke.Services.Scheduling;
using DotNetNuke.Services.Localization;
using System.Data;


namespace ICG.Modules.SecureMyInstall
{
    public partial class View : PortalModuleBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                if (!IsPostBack)
                {
                    //Have they started yet?
                    if (Settings["Phase1Complete"] == null)
                    {
                        btnPhase1.Enabled = true;
                        btnPhase2.Enabled = false;
                        btnPromote.Enabled = false;
                    }
                    else
                    {
                        //Phase 1 already complete
                        btnPhase1.Enabled = false;

                        //If superuser disable promote
                        if (UserInfo.IsSuperUser)
                            btnPromote.Enabled = false;
                        else
                            btnPromote.Enabled = true;

                        //See if Phase 2 started yet
                        if (Settings["Phase2Complete"] != null)
                        {
                            btnPhase2.Enabled = false;
                        }
                    }

                    //Load configuration stuffy
                    var xmlConfig = new XmlDocument();
                    xmlConfig = DotNetNuke.Common.Utilities.Config.Load();
                    var xmlMachineKey = xmlConfig.SelectSingleNode("configuration/system.web/machineKey");
                    lblValidationKey.Text = xmlMachineKey.Attributes["validationKey"].Value;
                    lblDecryptionKey.Text = xmlMachineKey.Attributes["decryptionKey"].Value;

                    var xmlMembershipProvider = xmlConfig.SelectSingleNode("configuration/system.web/membership/providers/add[@name='AspNetSqlMembershipProvider']");
                    if (!xmlMembershipProvider.Attributes["passwordFormat"].Value.Equals("Encrypted"))
                    {
                        //Are we in progress
                        if (Settings["Phase1Complete"] != null)
                        {
                            //If we are not done
                            if (Settings["Phase2Complete"] != null)
                                DotNetNuke.UI.Skins.Skin.AddModuleMessage(this, "You have already convered the passwords on this system", DotNetNuke.UI.Skins.Controls.ModuleMessage.ModuleMessageType.YellowWarning);
                        }
                        else
                        {
                            btnPhase1.Enabled = false;
                            btnPhase2.Enabled = false;
                            btnPromote.Enabled = false;
                            DotNetNuke.UI.Skins.Skin.AddModuleMessage(this,
                                                                      "You can only use this module if the passwords are currently encrypted",
                                                                      DotNetNuke.UI.Skins.Controls.ModuleMessage.
                                                                          ModuleMessageType.RedError);
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Exceptions.ProcessModuleLoadException(this, ex);
            }
        }

        /// <summary>
        /// Handles the Click event of the btnPhase1 control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        protected void btnPhase1_Click(object sender, EventArgs e)
        {
            var objScheduleItem = CreateScheduleItem("ICG.Modules.SecureMyInstall.Components.Phase1Job,  ICG.Modules.SecureMyInstall");
            SchedulingProvider.Instance().RunScheduleItemNow(objScheduleItem);
            DotNetNuke.UI.Skins.Skin.AddModuleMessage(this, "Phase 1 Started - Please Monitor Event Log For Progress, as well as the machine key values, once complete you will be logged off.", DotNetNuke.UI.Skins.Controls.ModuleMessage.ModuleMessageType.BlueInfo);

            //Add setting
            var oController = new ModuleController();
            oController.UpdateModuleSetting(ModuleId, "Phase1Complete", "1");
            btnPhase1.Enabled = false;
        }

        /// <summary>
        /// Handles the Click event of the btnPromote control.
        /// </summary>
        /// <remarks>
        /// Used to promote the current user to be a DotNetNuke Superuser
        /// </remarks>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        protected void btnPromote_Click(object sender, EventArgs e)
        {
            var sqlStatement = "UPDATE dbo.Users SET IsSuperUser = 1 WHERE Username = '" + UserInfo.Username + "'";
            DataProvider.Instance().ExecuteSQL(sqlStatement);
            Config.Touch();
            Response.Redirect(Request.RawUrl);
        }

        /// <summary>
        /// Handles the Click event of the btnPhase2 control.
        /// </summary>
        /// <remarks>
        /// Used to trigger the second part of the process.
        /// </remarks>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        protected void btnPhase2_Click(object sender, EventArgs e)
        {
            var objScheduleItem = CreateScheduleItem("ICG.Modules.SecureMyInstall.Components.Phase2Job,  ICG.Modules.SecureMyInstall");
            SchedulingProvider.Instance().RunScheduleItemNow(objScheduleItem);
            DotNetNuke.UI.Skins.Skin.AddModuleMessage(this, "Phase 2 Started - Please Monitor Event Log For Progress, once complete you will be logged off.", DotNetNuke.UI.Skins.Controls.ModuleMessage.ModuleMessageType.BlueInfo);

            //Add setting
            var oController = new ModuleController();
            oController.UpdateModuleSetting(ModuleId, "Phase2Complete", "2");
            btnPhase2.Enabled = false;
        }

        #region Helper Methods
        /// <summary>
        /// Creates the schedule item.
        /// </summary>
        /// <param name="typeFullName">Full name of the type.</param>
        /// <returns></returns>
        private ScheduleItem CreateScheduleItem(string typeFullName)
        {
            var objScheduleItem = new ScheduleItem();
            objScheduleItem.TypeFullName = typeFullName;
            objScheduleItem.FriendlyName = typeFullName;
            objScheduleItem.TimeLapse = 1;
            objScheduleItem.TimeLapseMeasurement = "d";
            objScheduleItem.RetryTimeLapse = Null.NullInteger;
            objScheduleItem.RetryTimeLapseMeasurement = "";
            objScheduleItem.RetainHistoryNum = 10;
            objScheduleItem.AttachToEvent = string.Empty;
            objScheduleItem.CatchUpEnabled = false;
            objScheduleItem.Enabled = true;
            objScheduleItem.ObjectDependencies = string.Empty;
            objScheduleItem.Servers = Null.NullString;

            var strSQL = "SELECT * FROM dbo.Schedule WHERE TypeFullName = '" + typeFullName + "'";
            IDataReader dr;
            dr = DataProvider.Instance().ExecuteSQL(strSQL);
            if(dr.Read())
                objScheduleItem.ScheduleID = dr.GetInt32(dr.GetOrdinal("ScheduleID"));

            return objScheduleItem;
        }
        #endregion

    }
}