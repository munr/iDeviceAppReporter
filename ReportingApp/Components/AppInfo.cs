using System;

namespace ReportingApp
{
	public class AppInfo
	{
		public int MessageNumber { get; set; }
		public int AppNumber { get; set; }

		public string AppName { get; set; }
		public string AppVersion { get; set; }
		public string AppSeller { get; set; }
		public string AppPrice { get; set; }

		public string OrderNumber { get; set; }
		public string ReceiptNumber { get; set; }
		public DateTime ReceiptDate { get; set; }
		public string OrderTotal { get; set; }
		public string BilledTo { get; set; }
	}
}
