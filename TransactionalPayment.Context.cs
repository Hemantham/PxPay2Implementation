using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;
using BIG4.Framework.Entities.Enums;
using BIG4.Framework.Entities.Models;
using BIG4.Framework.Entities.Models.Payment;
using PaymentExpressXML;
using BIG4.Framework.Entities;

namespace BIG4.Framework.Data
{
    public interface ITransactionalPaymentContext
    {
        PxPay2PaymentTransaction GetTransaction(PxPay2PaymentTransactionRequest input);

        ICreditCardPaymentResponse CompleteTransaction(string result);
    }

    public class PxPay2PaymentContext : ITransactionalPaymentContext
    {
        private string webServiceUrl = ConfigurationManager.AppSettings["PaymentExpress.PxPay"];
        private string username;
        private string password;

        public PxPay2PaymentContext(string username , string key)
        {
            this.username = username;
            this.password = key;

        }

        public PxPay2PaymentTransaction GetTransaction(PxPay2PaymentTransactionRequest input)
        {
            

            var response =  new PxPay2PaymentTransactionResponse(SubmitXml(GenerateTransactionRequest(input)));
            var result = new PxPay2PaymentTransaction {PaymentUrl = response.Url , TransactionId = input.TxnId};
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public ICreditCardPaymentResponse CompleteTransaction(string result)
        {
            var response = new PxPay2PaymentResponse(SubmitXml(GenerateTransactionCompleteRequest(result)));

            return new CreditCardPaymentResponse
                   {
                       AuthCode = response.AuthCode,
                       BillingId = response.BillingId,
                       CardHolderName = response.CardHolderName,
                       CardNumber = response.CardNumber,
                       MonthExpiry = short.Parse(response.DateExpiry.Substring(0, 2)),
                       YearExpiry = short.Parse(response.DateExpiry.Substring(2)),
                       IsSuccess = response.Success == "1",
                       ResponseText = response.ResponseText,
                       Settlement = string.IsNullOrEmpty(response.AmountSettlement) ? 0m : decimal.Parse(response.AmountSettlement),
                       TransactionId = response.TxnId,
                       TransactionType = response.TxnType,
                       CardType = (CreditCardType) Enum.Parse( typeof(CreditCardType), response.CardName.ToUpper() ),
                       SettlementDate = new DateTime(int.Parse(  response.DateSettlement.Substring(0, 4)), int.Parse(  response.DateSettlement.Substring(4, 2)), int.Parse(  response.DateSettlement.Substring(6, 2)))
                   };

        }
        
        /// <summary>
        /// Generates the XML required for a GenerateRequest call
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private string GenerateTransactionRequest(PxPay2PaymentTransactionRequest input)
        {
            var stringWriter = new StringWriter();

            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.NewLineOnAttributes = false;
            settings.OmitXmlDeclaration = true;

            using (XmlWriter writer = XmlWriter.Create(stringWriter, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("GenerateRequest");
                writer.WriteElementString("PxPayUserId", username);
                writer.WriteElementString("PxPayKey", password);

                PropertyInfo[] properties = input.GetType().GetProperties();

                foreach (PropertyInfo prop in properties)
                {
                    if (prop.CanWrite)
                    {
                        var val = (string)prop.GetValue(input, null);

                        if (!string.IsNullOrEmpty(  val ))
                        {

                            writer.WriteElementString(prop.Name, val);
                        }
                    }
                }
                writer.WriteEndElement();
                writer.WriteEndDocument();
                writer.Flush();
            }

            return stringWriter.ToString();
        }

        /// <summary>
        /// Generates the XML required for a completing transaction 
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private string GenerateTransactionCompleteRequest(string result)
        {
            var stringWriter = new StringWriter();

            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.NewLineOnAttributes = false;
            settings.OmitXmlDeclaration = true;

            using (XmlWriter writer = XmlWriter.Create(stringWriter, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("ProcessResponse");
                writer.WriteElementString("PxPayUserId", username);
                writer.WriteElementString("PxPayKey", password);
                writer.WriteElementString("Response", result);
                writer.WriteEndElement();
                writer.WriteEndDocument();
                writer.Flush();
            }

            return stringWriter.ToString();
        }

        private string SubmitXml(string inputXml)
        {
            var webRequest = (HttpWebRequest)WebRequest.Create(webServiceUrl);
            webRequest.Method = "POST";

            var reqBytes  = Encoding.UTF8.GetBytes(inputXml);
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.ContentLength = reqBytes.Length;
            webRequest.Timeout = 5000;
            Stream requestStream = webRequest.GetRequestStream();
            requestStream.Write(reqBytes, 0, reqBytes.Length);
            requestStream.Close();

            var webResponse = (HttpWebResponse)webRequest.GetResponse();
            using (var streamReader = new StreamReader(webResponse.GetResponseStream(), Encoding.ASCII))
            {
                return streamReader.ReadToEnd();
            }
        }

      
    }

    
}
