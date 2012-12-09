using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ImapX;
using Message = ImapX.Message;

namespace ReportingApp
{
	internal delegate void UpdateUITextDelegate(string message);

	public partial class MainForm : Form
	{
		#region Fields

		private readonly SortableBindingList<AppInfo> m_AppInfo = new SortableBindingList<AppInfo>();
		private int m_AppCount = 0;
		private double m_TotalCost = 0;
		private bool m_Cancel = false;

		#endregion

		#region Constructor

		public MainForm()
		{
			InitializeComponent();
		}

		#endregion

		#region Form Events

		private void btnGo_Click(object sender, EventArgs e)
		{
			logRTB.Clear();
			simpleListRTB.Clear();
			cancelButton.Enabled = true;

			m_Cancel = false;

			UpdateStatusBar("Processing...");
			backgroundWorker1.RunWorkerAsync();
		}

		private void cancelButton_Click(object sender, EventArgs e)
		{
			m_Cancel = true;
			backgroundWorker1.CancelAsync();
			MessageBox.Show("Report Generation Cancelled", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
		}

		private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
		{
			ProcessEmails();
		}

		private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			cancelButton.Enabled = false;
			dataGridView1.DataSource = m_AppInfo;
		}

		private void copyToolStripMenuItem_Click(object sender, EventArgs e)
		{
			string message = string.Empty;

			if (contextMenuStrip1.SourceControl is RichTextBox)
			{
				RichTextBox rtb = (RichTextBox) contextMenuStrip1.SourceControl;

				if (rtb.SelectedText.Length > 0)
				{
					Clipboard.SetData(DataFormats.Text, rtb.SelectedText);
					message = "Selected text copied to clipboard";
				}
				else
				{
					Clipboard.SetData(DataFormats.Text, rtb.Text);
					message = "All text copied to clipboard";
				}
			}

			if (contextMenuStrip1.SourceControl is DataGridView)
			{
				DataGridView dgv = (DataGridView) contextMenuStrip1.SourceControl;
				string text = dgv.SelectedCells[0].Value.ToString();
				Clipboard.SetData(DataFormats.Text, text);
				message = "Copied cell value to clipboard";
			}

			if (!string.IsNullOrEmpty(message))
				MessageBox.Show(message, "Copied Text", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
		}

		#endregion

		private void ProcessEmails()
		{
			// using IMAPX library
			// http://hellowebapps.com/products/imapx/

			string username = txtUsername.Text;
			string password = txtPassword.Text;

			try
			{
				ImapClient client = new ImapClient(txtIMAPServer.Text, GetUserEnteredPort(), chkSSL.Checked);

				AddMessage("Initialized");

				bool result = client.Connection();

				if (result)
				{
					AddMessage("Connected to server");
				}
				else
				{
					AddMessage("Unable to connect to server");
					return;
				}

				result = client.LogIn(username, password);

				if (!result)
				{
					AddMessage("Unable to login. Check login details.");
					return;
				}

				AddMessage("Logged in");

				Folder folder = client.Folders[txtFolder.Text];

				if (folder == null)
				{
					AddMessage("Folder not found. Check folder name.");
					return;
				}

				MessageCollection messageCollection = folder.Search("FROM \"iTunes Store\"", false);

				AddMessage("Found " + messageCollection.Count + " messages");

				AddMessage("***************************");

				int count = 0;

				foreach (Message message in messageCollection)
				{
					if (m_Cancel)
					{
						AddMessage("Cancelled");
						AddItemToSimpleRTB("\nCancelled");
						return;
					}

					count++;

					message.Process();

					string from = "unknown";

					if (message.From.Count > 0)
						from = message.From[0].Address;

					AddMessage("Processing Message " + count + " of " + messageCollection.Count + " : " + from + ", " + message.Subject);

					ProcessMessage(count, message);
				}

				AddMessage("Done");

				UpdateStatusBar(string.Format("Finished processing {0} messages. Found {1} apps.  Total cost: ${2}", messageCollection.Count, m_AppCount, m_TotalCost.ToString("N2")));
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void ProcessMessage(int number, Message message)
		{
			// Get the email text body
			string messageBody = message.TextBody.TextData;

			// Replace any =\n with spaces.  This is some weird formatting that
			// happens in a few random emails fro no apparent reason
			messageBody = messageBody.Replace("=" + Environment.NewLine, string.Empty);

			// Get the message header
			MessageHeader header = MessageHeader.Get(message);

			if (string.IsNullOrEmpty(header.ReceiptNumber) && string.IsNullOrEmpty(header.OrderNumber))
				return;

			// Parse the message and get the app item lines
			IEnumerable<string> itemLines = GetItemLines(messageBody);

			foreach (string itemLine in itemLines)
				AddItemToSimpleRTB(itemLine + Environment.NewLine);

			int count = 0;

			foreach (string itemLine in itemLines)
			{
				string line = itemLine;

				if (line.EndsWith("Free"))
					line = line.Substring(0, itemLine.Length - 4) + "$0.00";

				m_AppCount++;
				m_TotalCost += GetPrice(itemLine);

				UpdateStatusBar(string.Format("Found {0} apps... Cost: ${1}", m_AppCount, m_TotalCost.ToString("N2")));

				// Parse the line
				Match match = Regex.Match(line, @"(?<ItemCode>[^\s\s]+)\s+(?<AppName>[^,]+),\sv(?<AppVersion>[0-9A-Za-z\.]+),\sSeller:\s{2,}(?<Seller>[^$]+)(?<Price>[$\d\.\D]+)");

				// Get matched values
				string appName = match.Groups["AppName"].Value.Trim();
				string appVersion = match.Groups["AppVersion"].Value.Trim();
				string appSeller = match.Groups["Seller"].Value.Trim();
				string appPrice = match.Groups["Price"].Value.Trim();

				// Hacky workaround, default app name to line if no match
				if (!match.Success)
					appName = line.Substring(line.IndexOf(" ")).Trim();

				var o = new AppInfo
				        	{
				        		MessageNumber = number,
				        		AppNumber = m_AppCount,

				        		AppName = appName,
				        		AppVersion = appVersion,
				        		AppSeller = appSeller,
				        		AppPrice = appPrice,

				        		ReceiptNumber = header.ReceiptNumber,
				        		OrderNumber = header.OrderNumber,
				        		ReceiptDate = header.ReceiptDate,
				        		OrderTotal = header.OrderTotal,
				        		BilledTo = header.BilledTo
				        	};

				m_AppInfo.Add(o);

				count++;
			}

			AddMessage("Found " + count + " apps");
		}

		private int GetUserEnteredPort()
		{
			try
			{
				return Convert.ToInt32(txtPort.Text);
			}
			catch
			{
				return 443;
			}
		}

		private static IEnumerable<string> GetItemLines(string messageBody)
		{
			string[] lines = messageBody.Split('\n');

			bool read = false;
			bool skip = false;

			foreach (string line in lines)
			{
				if (skip)
				{
					skip = false;
					continue;
				}

				if (line.StartsWith("Item Number"))
				{
					skip = true;
					read = true;
					continue;
				}

				if (read && line.StartsWith("----------"))
					read = false;

				if (read && line.Trim().Length > 0)
					yield return line.Trim();
			}
		}

		private static double GetPrice(string itemLine)
		{
			double price = 0d;

			Match match = Regex.Match(itemLine, @"\$(?<Price>\d{1,2}\.\d{2,})");

			if (match.Success)
				price = Convert.ToDouble(match.Groups["Price"].Value);

			return price;
		}

		#region Private UI Update Methods

		private void UpdateStatusBar(string message)
		{
			if (InvokeRequired)
			{
				UpdateUITextDelegate d = UpdateStatusBar;
				Invoke(d, new[] { message });
			}
			else
			{
				toolStripStatusLabel1.Text = message;
				toolStripStatusLabel1.Invalidate();
			}
		}

		private void AddItemToSimpleRTB(string s)
		{
			if (InvokeRequired)
			{
				UpdateUITextDelegate d = AddItemToSimpleRTB;
				Invoke(d, new[] { s });
			}
			else
			{
				simpleListRTB.AppendText(s);
				simpleListRTB.Refresh();
				simpleListRTB.ScrollToCaret();
			}
		}

		private void AddMessage(string message)
		{
			if (InvokeRequired)
			{
				UpdateUITextDelegate d = AddMessage;
				Invoke(d, new [] { message });
			}
			else
			{
				string text = DateTime.Now.ToString("dd MMM yyyy HH:mm:ss") + " : " + message + Environment.NewLine;
				logRTB.AppendText(text);
				logRTB.Refresh();
				logRTB.ScrollToCaret();
			}
		}

		#endregion
	}
}