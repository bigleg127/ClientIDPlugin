using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Crm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace ClientIdPlugin
{
    public class ClientIdPlugin: IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {

            var tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var orgservice = serviceFactory.CreateOrganizationService(context.UserId);

            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity)) return;
            if (tracingService == null) return;

            var account = (Entity)context.InputParameters["Target"];
            if (account.LogicalName != "account") return;

            account.Attributes["ergo_clientid"] = ComputeMainClientID(account.Attributes["name"].ToString(), orgservice);
            orgservice.Update(account);
            return;
            
        }

        private static string ComputeMainClientID(string AccountName, IOrganizationService orgservice)
        {
            var result = new StringBuilder();
            var counter = 1;
            var maxClientIdLength = 35;

            //Change string to all lowercase
            AccountName = AccountName.ToLower();

            //Cycle through the string and remove all non alpha-numeric characters
            for (int i = 0; i <= AccountName.Length - 1; i++)
            {
                var c = AccountName[i];
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                    result.Append(c);
            }

            //Make sure the string is between 2 and maxClientIdLength characters long, If it is over maxClientIdLength truncate it and if it is below add padding
            if (result.Length > maxClientIdLength)
                result = result.Remove(maxClientIdLength, result.Length - maxClientIdLength);
            else if (result.Length < 2)
                result.Append("_");

            //Check existing accounts to make sure that the string is currently not already used in ergo_clientid
            //We append an incremental digit to the end of the string until we find a suitable clientid
            while(isDuplicate(result.ToString(), orgservice))
            {
                if (counter != 1 | result.Length + counter.ToString().Length > maxClientIdLength) 
                    result.Length = result.Length - counter.ToString().Length;
                result.Append(counter);
                counter++;
            }

            return result.ToString();
        }

        private static bool isDuplicate(string clientid, IOrganizationService orgservice)
        {
            //we search CRM to see if clientid is already in use and return true if it is and false otherwise
            var query = new QueryExpression("account");
            var columns = new ColumnSet();
            var filter = new FilterExpression();

            columns.AddColumn("ergo_clientid");
            filter.AddCondition("ergo_clientid", ConditionOperator.Equal, clientid);
            query.ColumnSet = columns;
            query.Criteria.AddFilter(filter);

            if(orgservice.RetrieveMultiple(query).Entities.Any()) return true;
            return false;
        }
    }
}
