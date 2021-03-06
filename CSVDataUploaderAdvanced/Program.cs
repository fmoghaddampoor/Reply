﻿using System.Configuration;
using System.ServiceModel.Description;
using System.Net;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Text;
using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using System.Threading.Tasks;

namespace CSVDataUploaderAdvanced
{
    class Program
    {
        static void Main(string[] args)
        {
            var organizationServiceProxy = new OrganizationServiceProxy(GetUri(), null, GetClientCredentials(), null);
            DataTable csvDataTable = GetCSVData();
            ExecuteMultipleRequest executeMultipleRequest1 = new ExecuteMultipleRequest()
            {
                Settings = new ExecuteMultipleSettings
                {
                    ContinueOnError = true,
                    ReturnResponses = true
                }
                ,
                Requests = new OrganizationRequestCollection()
            };
            ExecuteMultipleRequest executeMultipleRequest2 = new ExecuteMultipleRequest()
            {
                Settings = new ExecuteMultipleSettings
                {
                    ContinueOnError = true,
                    ReturnResponses = true
                }
                ,
                Requests = new OrganizationRequestCollection()
            };
            int counter = 0;
            for (int locIndexRow=1; locIndexRow< csvDataTable.Rows.Count; locIndexRow++)
            {
                string name = csvDataTable.Rows[locIndexRow][0].ToString();
                string primaryContact = csvDataTable.Rows[locIndexRow][1].ToString();
                string telephone1 = csvDataTable.Rows[locIndexRow][2].ToString();
                QueryExpression queryExpression = new QueryExpression("contact");
                queryExpression.ColumnSet = new ColumnSet(false);
                queryExpression.NoLock = true;
                queryExpression.TopCount = 1;
                queryExpression.Criteria.AddCondition("fullname", ConditionOperator.Equal, primaryContact);
                Entity queryResult = organizationServiceProxy.RetrieveMultiple(queryExpression).Entities.FirstOrDefault();
                if (queryResult!=null)
                {
                    EntityReference primaryContactReference = queryResult.ToEntityReference();
                    Entity customEntity = CreateCustomEntity("account", name, primaryContactReference, telephone1);
                    UpsertRequest upsertRequest = new UpsertRequest()
                    {
                        Target = customEntity
                    };
                    if(locIndexRow % 2 == 0)
                    {
                        executeMultipleRequest1.Requests.Add(upsertRequest);
                    }
                    else
                    {
                        executeMultipleRequest2.Requests.Add(upsertRequest);
                    }
                    counter++;
                    Console.WriteLine($"Request {counter} added");
                }
            }
            Console.WriteLine("Started executing request.");
            Task.WaitAll(Task.Run(()=> 
            {
                ExecuteMultipleResponse executeMultipleResponse = (ExecuteMultipleResponse)organizationServiceProxy.Execute(executeMultipleRequest1); 
                foreach(var response in executeMultipleResponse.Responses)
                {
                    if(response.Fault!=null)
                    {
                        Console.WriteLine(response.Fault.Message);
                    }
                    else
                    {
                        Console.WriteLine("Request1 executed done!");
                    }
                }
   
            }),
            Task.Run(() =>
            {
                ExecuteMultipleResponse executeMultipleResponse = (ExecuteMultipleResponse)organizationServiceProxy.Execute(executeMultipleRequest2);
                foreach (var response in executeMultipleResponse.Responses)
                {
                    if (response.Fault != null)
                    {
                        Console.WriteLine(response.Fault.Message);
                    }
                    else
                    {
                        Console.WriteLine("Request2 executed done!");
                    }
                }

            }));
            executeMultipleRequest1.Requests.Clear();
            executeMultipleRequest2.Requests.Clear();
            Console.WriteLine("Requests have been executed");
            Console.ReadLine();
        }

        private static Entity CreateCustomEntity(string tableName, string name, EntityReference primaryContact , string telephone1)
        {
            return new Entity(tableName)
            {
                ["name"] = name,
                ["primarycontactid"] = primaryContact,
                ["telephone1"] = telephone1
            };
        }

        private static ClientCredentials GetClientCredentials()
        {
            ClientCredentials clientCredentials = new ClientCredentials();
            clientCredentials.UserName.UserName = ConfigurationManager.ConnectionStrings["user"].ToString();
            clientCredentials.UserName.Password = ConfigurationManager.ConnectionStrings["password"].ToString();
            return clientCredentials;
        }

        private static SecurityProtocolType GetSecurityProtocolType()
        {
            return SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
        }

        private static System.Uri GetUri()
        {
            return new System.Uri(ConfigurationManager.ConnectionStrings["url"].ToString());
        }

        private static string GetFilePath()
        {
            return ConfigurationManager.ConnectionStrings["ExcelCSVPath"].ToString();
        }

        private static DataTable GetCSVData()
        {
            DataTable dt = null;
            string[] csvLines = ReadFileLines(GetFilePath());
            if(csvLines.Length > 0)
            {
                char delimiter = GetFileDelimiter(csvLines[0]);
                dt = new DataTable();
                for (int locIndexLine = 0; locIndexLine < csvLines.Length; locIndexLine++)
                {
                    string[] cells = csvLines[locIndexLine].Split(new char[] { delimiter });
                    dt.Rows.Add();
                    for (int locIndexPart=0; locIndexPart< cells.Length; locIndexPart++)
                    {
                        dt.Columns.Add();
                        dt.Rows[locIndexLine][locIndexPart] = cells[locIndexPart];
                    }
                }
            }
            return dt;
        }

        private static string[] ReadFileLines(string fileName)
        {
            List<string> lstLines = new List<string>();
            using (System.IO.FileStream fileStream = new System.IO.FileStream(fileName, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            {
                using (System.IO.StreamReader sr = new System.IO.StreamReader(fileStream,Encoding.UTF8))
                {
                    string line = String.Empty;
                    while ((line = sr.ReadLine()) != null)
                    {
                        lstLines.Add(line);
                    }
                }
            }
            return lstLines.ToArray();
        }

        private static char GetFileDelimiter(string input)
        {
            List<char> delimiters = new List<char> { ' ', ';', '-' , ',', '|'};
            Dictionary<char, int> dicCharCount = delimiters.ToDictionary(key => key, value => 0);
            foreach (char delimiter in delimiters)
            {
                dicCharCount[delimiter] = input.Count(t => t == delimiter);
            }
            return dicCharCount.OrderByDescending(x => x.Value).First().Key;
        }
    }
}
