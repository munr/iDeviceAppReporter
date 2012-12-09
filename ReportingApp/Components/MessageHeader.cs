using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using ImapX;

namespace ReportingApp
{
	internal class MessageHeader
	{
		public string ReceiptNumber { get; set; }
		public DateTime ReceiptDate { get; set; }
		public string OrderNumber { get; set; }
		public string OrderTotal { get; set; }
		public string BilledTo { get; set; }

		public static MessageHeader Get(Message message)
		{
			MessageHeader header = new MessageHeader();

			// Assume we don't know the receipt number
			header.ReceiptNumber = "[unknown]";

			// Try and get the receipt from the email subject line
			if (message.Subject.Contains("#"))
				header.ReceiptNumber = message.Subject.Substring(message.Subject.IndexOf("#")).Trim();

			// Get message text body
			string textBody = message.TextBody.TextData;

			// Parse body for values
			header.OrderNumber = GetValueFromMessage(textBody, "Order Number");
			header.OrderTotal = GetValueFromMessage(textBody, "Order Total");
			header.BilledTo = GetValueFromMessage(textBody, "Billed To");

			try
			{
				string datestring = GetValueFromMessage(textBody, "Receipt Date");
				header.ReceiptDate = Convert.ToDateTime(datestring);
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Unable to get date", ex.Message);
				header.ReceiptDate = DateTime.MinValue;
			}

			return header;
		}

		private static string GetValueFromMessage(string message, string prefix)
		{
			Match match = Regex.Match(message, prefix.Replace(" ", "\\s") + @":\s(?<Val>.*)");
			string val = match.Groups["Val"].Value;

			val = val.Trim();
			val = val.Replace("=20", string.Empty);
			val = val.Replace("=C2=A3", "$"); // Replace £ with $ for convenience
			val = val.Trim();

			return val;
		}
	}
}