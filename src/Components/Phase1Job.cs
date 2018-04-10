using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Xml;
using DotNetNuke.Services.Scheduling;
using Microsoft.ApplicationBlocks.Data;
using DotNetNuke.Common.Utilities;
using System.Data;
using DotNetNuke.Services.Log.EventLog;
using DotNetNuke.Security;


namespace ICG.Modules.SecureMyInstall.Components
{
    public class Phase1Job : SchedulerClient
    {

        #region Constructors

        public Phase1Job(ScheduleHistoryItem historyItem) : base()
        {
            this.ScheduleHistoryItem = historyItem;
        }
        #endregion

        public override void DoWork()
        {
            try
            {
                //Notify of start
                this.Progressing();

                //Complete it
                PerformStepOne();

                //Notify of success
                this.ScheduleHistoryItem.Succeeded = true;
            }
            catch (Exception ex)
            {
                this.ScheduleHistoryItem.Succeeded = false;
                InsertLogNote("Exception= " + ex.ToString());
                this.Errored(ref ex);
                DotNetNuke.Services.Exceptions.Exceptions.LogException(ex);
            }
            
        }

        public void PerformStepOne()
        {
            IDataReader dr;
            string sqlStatement;
            var objLogger = new DotNetNuke.Services.Log.EventLog.EventLogController();
            LogInfo objLog;

            InsertLogNote("Starting Phase 1 Processing");

            //Remove temporary table if needed
            sqlStatement = "DROP TABLE dbo.[ICG_SMI_Passwords]";
            try
            {
                ExecuteNonQuery(sqlStatement);
            }
            catch(Exception)
            {
                //Table didn't exist
            }

            //Create temporary table
            sqlStatement = "CREATE TABLE dbo.[ICG_SMI_Passwords]( [UserId] [uniqueidentifier] NOT NULL, [Username] [nvarchar](256) NOT NULL, [OldPassword] [nvarchar](128) NOT NULL, [Password] [nvarchar](128) NULL, CONSTRAINT [PK_aspnet_Membership_Passwords] PRIMARY KEY CLUSTERED ( [UserId] ASC ) ON [PRIMARY] ) ON [PRIMARY]";
            try
            {
                ExecuteNonQuery(sqlStatement);
                objLog = new LogInfo();
                objLog.AddProperty("SecureMyInstall", "Phase 1 Created Temp Password Table");
                objLog.LogTypeKey = EventLogController.EventLogType.HOST_ALERT.ToString();
                objLogger.AddLog(objLog);

            }
            catch(Exception ex)
            {
                objLog = new LogInfo();
                objLog.AddProperty("SecureMyInstall", "Phase 1 Error Creating Temp Password Table " + ex);
                objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString();
                objLogger.AddLog(objLog);
            }
            
            InsertLogNote("Created temp password table");

            //Migrate over passwords
            sqlStatement = "INSERT INTO dbo.ICG_SMI_Passwords (UserId, Username, OldPassword, Password) SELECT dbo.aspnet_Users.UserId, Username, Password, NULL FROM dbo.aspnet_Users INNER JOIN dbo.aspnet_Membership ON dbo.aspnet_Users.UserId = dbo.aspnet_Membership.UserId";
            try
            {
                ExecuteNonQuery(sqlStatement);
                objLog = new DotNetNuke.Services.Log.EventLog.LogInfo();
                objLog.AddProperty("SecureMyInstall", "Phase 1 Inserted User Accounts Into Temp Password Table");
                objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString();
                objLogger.AddLog(objLog);
            }
            catch(Exception ex)
            {
                objLog = new DotNetNuke.Services.Log.EventLog.LogInfo();
                objLog.AddProperty("SecureMyInstall", "Phase 1 Error Inserting User Accounts Into Temp Password Table " + ex.ToString());
                objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString();
                objLogger.AddLog(objLog);
            }

            InsertLogNote("Migrated passwords to temporary table");

            //Decrypt passwords
            int processedUsers = 0;
            int decryptedUsers = 0;
            System.Web.Security.MembershipUser objUser;
            var wasSuccessful = false;

            sqlStatement = "SELECT * FROM dbo.ICG_SMI_Passwords";

            try
            {
                dr = ExecuteReader(sqlStatement);
                while(dr.Read())
                {
                    processedUsers ++;
                    objUser = Membership.GetUser(dr.GetString(dr.GetOrdinal("Username")));
                    if(objUser != null)
                    {
                        try
                        {
                            var strPassword = objUser.GetPassword();
                            sqlStatement = "UPDATE dbo.ICG_SMI_Passwords SET Password = '" + strPassword.Replace("'", "''") + "' WHERE Username = '" + dr.GetString(dr.GetOrdinal("Username")) + "'";
                            ExecuteNonQuery(sqlStatement);
                            decryptedUsers ++;
                        }
                        catch(Exception)
                        {
                            //Unable to get this users password
                        }
                    }

                    //Provide status
                    if(processedUsers != 0 && (processedUsers % 1000) == 0)
                    {
                        objLog = new DotNetNuke.Services.Log.EventLog.LogInfo();
                        objLog.AddProperty("SecureMyInstall", "Phase 1 User Accounts Processed: " + processedUsers.ToString());
                        objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString();
                        objLogger.AddLog(objLog);
                    }
                }
                dr.Close();
                InsertLogNote(string.Format("Processed {0} user accounts", processedUsers.ToString()));
                InsertLogNote(string.Format("Decrypted {0} user password", decryptedUsers.ToString()));
                wasSuccessful = true;
            }
            catch(Exception)
            {
                //Oopsie
            }

            //Final eventLog entries
            objLog = new DotNetNuke.Services.Log.EventLog.LogInfo();
            objLog.AddProperty("SecureMyInstall", "Phase 1 User Accounts Processed: " + processedUsers.ToString());
            objLog.AddProperty("SecureMyInstall", "Phase 1 User Passwords Decrypted: " + decryptedUsers.ToString());
            objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString();
            objLogger.AddLog(objLog);

            if(wasSuccessful)
            {
                //Generate new keys
                var objSecurity = new PortalSecurity();
                var newValidationKey = objSecurity.CreateKey(20);
                var newDecryptionKey = objSecurity.CreateKey(24);
                objLog = new DotNetNuke.Services.Log.EventLog.LogInfo();
                objLog.AddProperty("SecureMyInstall", "Phase 1 New Machine Keys Generated");
                objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString();
                objLogger.AddLog(objLog);
                InsertLogNote("Phase 2 New Machine Keys Generated");

                //Update web.config, change keys as well as password format
                try
                {
                    DotNetNuke.Common.Utilities.Config.BackupConfig();
                    var xmlConfig = new XmlDocument();
                    xmlConfig = DotNetNuke.Common.Utilities.Config.Load();
                    var xmlMachineKey = xmlConfig.SelectSingleNode("configuration/system.web/machineKey");
                    XmlUtils.UpdateAttribute(xmlMachineKey, "validationKey", newValidationKey);
                    XmlUtils.UpdateAttribute(xmlMachineKey, "decryptionKey", newDecryptionKey);
                    var xmlMembershipProvider = xmlConfig.SelectSingleNode("configuration/system.web/membership/providers/add[@name='AspNetSqlMembershipProvider']");
                    XmlUtils.UpdateAttribute(xmlMembershipProvider, "passwordFormat", "Hashed");
                    XmlUtils.UpdateAttribute(xmlMembershipProvider, "enablePasswordRetrieval", "false");
                    DotNetNuke.Common.Utilities.Config.Save(xmlConfig);
                    objLog = new DotNetNuke.Services.Log.EventLog.LogInfo();
                    objLog.AddProperty("SecureMyInstall", "Phase 1 Updated Web.Config With New Machine Keys and password format");
                    objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString();
                    objLogger.AddLog(objLog);
                }
                catch(Exception)
                {
                    objLog = new DotNetNuke.Services.Log.EventLog.LogInfo();
                    objLog.AddProperty("SecureMyInstall", "Phase 1 Error Processing User Accounts");
                    objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString();
                    objLogger.AddLog(objLog);
                }
            }
        }

        #region Log Helpers
        /// <summary>
        /// Helper method to keep the code uncluttered
        /// </summary>
        /// <param name="message"></param>
        private void InsertLogNote(string message)
        {
            this.ScheduleHistoryItem.AddLogNote(message + "<br />");
        }
        #endregion

        #region SQL Helpers
        /// <summary>
        /// Executes the non query.
        /// </summary>
        /// <param name="sqlStatement">The SQL statement.</param>
        private void ExecuteNonQuery(string sqlStatement)
        {
            SqlHelper.ExecuteNonQuery(Config.GetConnectionString(), CommandType.Text, sqlStatement);
        }

        /// <summary>
        /// Executes the reader.
        /// </summary>
        /// <param name="sqlStatement">The SQL statement.</param>
        /// <returns></returns>
        private IDataReader ExecuteReader(string sqlStatement)
        {
            return SqlHelper.ExecuteReader(Config.GetConnectionString(), CommandType.Text, sqlStatement);
        }
        #endregion
    }
}