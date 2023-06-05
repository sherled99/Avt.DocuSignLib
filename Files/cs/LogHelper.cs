using System;
using Terrasoft.Core;
using Terrasoft.Core.DB;

namespace Avt.DocuSignLib.Files.cs
{
    public class LogHelper
    {
        public void InsertLog(string log, UserConnection userConnection)
        {
            var insertLog = new Insert(userConnection.AppConnection.SystemUserConnection).Into("AvtDocuSignLog")
                .Set("Log", Column.Parameter(log))
                .Set("CreatedById", Column.Parameter(userConnection.CurrentUser.ContactId))
                .Set("CreatedOn", Column.Parameter(DateTime.Now));
            insertLog.Execute();
        }
    }
}
