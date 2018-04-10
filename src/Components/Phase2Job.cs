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
    public class Phase2Job : SchedulerClient
    {

        #region Constructors

        public Phase2Job(ScheduleHistoryItem historyItem) : base()
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
                PerformStepTwo();

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

        public void PerformStepTwo()
        {
            IDataReader dr;
            string sqlStatement;
            var objLogger = new EventLogController();
            LogInfo objLog;

            InsertLogNote("Phase 2 Starting");

            //Set user passwords
            int processedUsers = 0;
            int resetUsers = 0;
            MembershipUser objUser;
            var wasSuccessful = false;

            sqlStatement = "SELECT * FROM dbo.ICG_SMI_Passwords WHERE Password IS NOT NULL";
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
                            //Reset password, 
                            var tempPassword = objUser.ResetPassword();

                            //Now, set the password
                            objUser.ChangePassword(tempPassword, dr.GetString(dr.GetOrdinal("Password")));
                            resetUsers ++;
                        }
                        catch(Exception)
                        {
                            //One user failed, they can request forgotten password
                        }
                    }

                    //Provide status
                    if(processedUsers != 0 && (processedUsers % 1000) == 0)
                    {
                        objLog = new DotNetNuke.Services.Log.EventLog.LogInfo();
                        objLog.AddProperty("SecureMyInstall", "Phase 2 User Accounts Processed: " + processedUsers.ToString());
                        objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString();
                        objLogger.AddLog(objLog);
                    }
                }

                dr.Close();
                InsertLogNote(string.Format("Processed {0} user accounts", processedUsers.ToString()));
                InsertLogNote(string.Format("Updated {0} user passwords", resetUsers.ToString()));
                wasSuccessful = true;
            }
            catch(Exception)
            {
                //Skippy dee-doo-da-day
            }

            objLog = new DotNetNuke.Services.Log.EventLog.LogInfo();
            objLog.AddProperty("SecureMyInstall", "Phase 2 User Accounts Processed: " + processedUsers.ToString());
            objLog.AddProperty("SecureMyInstall", "Phase 2 User Passwords Updated: " + resetUsers.ToString());
            objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString();
            objLogger.AddLog(objLog);

            if(wasSuccessful)
            {
                //Drop table
                sqlStatement = "DROP TABLE dbo.[ICG_SMI_Passwords]";
                try
                {
                    ExecuteNonQuery(sqlStatement);
                    objLog = new DotNetNuke.Services.Log.EventLog.LogInfo();
                    objLog.AddProperty("SecureMyInstall", "Phase 2 Dropped Temp Password Table");
                    objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString();
                    objLogger.AddLog(objLog);
                }
                catch(Exception ex)
                {
                    objLog = new DotNetNuke.Services.Log.EventLog.LogInfo();
                    objLog.AddProperty("SecureMyInstall", "Phase 2 Error Dropping Temp Password Table " + ex.ToString());
                    objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString();
                    objLogger.AddLog(objLog);
                }

                try
                {
                    var xmlConfig = new XmlDocument();
                    xmlConfig = DotNetNuke.Common.Utilities.Config.Load();
                    var xmlForms = xmlConfig.SelectSingleNode("configuration/system.web/authentication/forms");
                    XmlUtils.UpdateAttribute(xmlForms, "name", ".DOTNETNUKE" + DateTime.Now.ToString("yyyyMMddhhss"));
                    DotNetNuke.Common.Utilities.Config.Save(xmlConfig);
                    objLog = new DotNetNuke.Services.Log.EventLog.LogInfo();
                    objLog.AddProperty("SecureMyInstall", "Phase 2 Updated Web.Config With New Forms Authentication Ticket Name");
                    objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString();
                    objLogger.AddLog(objLog);
                }
                catch(Exception ex)
                {
                    objLog = new DotNetNuke.Services.Log.EventLog.LogInfo();
                    objLog.AddProperty("SecureMyInstall", "Phase 2 Error Updating Web.Config With New Forms Authentication Ticket Name " + ex.ToString());
                    objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString();
                    objLogger.AddLog(objLog);
                }
            }
            else
            {
                objLog = new DotNetNuke.Services.Log.EventLog.LogInfo();
                objLog.AddProperty("SecureMyInstall", "Phase 2 Error Processing User Accounts");
                objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString();
                objLogger.AddLog(objLog);
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