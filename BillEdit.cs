using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection.Emit;
using System.Text;
using System.Windows.Forms;
using static PposSystem.sale_system;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace PposSystem
{
    public partial class BillEdit : Form
    {
        private string accessToken;
        private string billNumber;
        private double oldGrandTotal = 0.0;
        private string username;
        public BillEdit(string accessToken, string billNumber, string username)
        {
            InitializeComponent();
            this.accessToken = accessToken;
            this.billNumber = billNumber;
            this.username = username;
        }

        public class Item
        {
            public int id { get; set; }
            public int pid { get; set; }
            public string name { get; set; }
            public double selling_price { get; set; }
            public double qty { get; set; }
            public double subtotal { get; set; }
        }

        public class ApiResponse
        {
            public int id { get; set; }
            public string payment_method { get; set; }
            public string created_at { get; set; }
            public List<ItemFromApi> items { get; set; }
            public string subtotal { get; set; }
            public string discount { get; set; }
            public string total { get; set; }
        }

        public class ItemFromApi
        {
            public int id { get; set; }
            public int product { get; set; }
            public string product_name { get; set; }
            public string unit_price { get; set; }
            public string quantity { get; set; }
            public string subtotal { get; set; }

        }
        public class SaleRequest
        {
            public string discount { get; set; }
            public string amount_paid { get; set; }
            public string correction_reason { get; set; }
            public List<SaleItem> items { get; set; }
        }

        public class SaleItem
        {
            public int product { get; set; }
            public string quantity { get; set; }
            public string unit_price { get; set; }
        }

        BindingList<Item> items = new BindingList<Item>();
        BindingSource itemsBindingSource;

        private void BillEdit_Load(object sender, EventArgs e)
        {
            label22.Text = "#" + billNumber;
            SetupTable();

            // Use BindingSource so grid updates automatically with list changes
            itemsBindingSource = new BindingSource();
            itemsBindingSource.DataSource = items;
            dataGridView1.DataSource = itemsBindingSource;

            // Hook events
            dataGridView1.EditingControlShowing += dataGridView1_EditingControlShowing;
            dataGridView1.CurrentCellDirtyStateChanged += dataGridView1_CurrentCellDirtyStateChanged;

            List<ItemFromApi> results = FetchResultsFromApi(billNumber);

            if (results != null)
            {
                foreach (var apiItem in results)
                {
                    items.Add(new Item()
                    {
                        id = apiItem.id,
                        pid = apiItem.product,
                        name = apiItem.product_name,
                        selling_price = Convert.ToDouble(apiItem.unit_price),
                        qty = Convert.ToDouble(apiItem.quantity),
                        subtotal = Convert.ToDouble(apiItem.subtotal)
                    });
                }
                // Initialize total cells
                for (int i = 0; i < items.Count; i++)
                {
                    UpdateTotal(i);
                }
            }
        }
        public class ApiSaleResponse
        {
            public string payment_method { get; set; }  // CASH / CARD
            public string discount { get; set; }        // Discount amount
            public string amount_paid { get; set; }     // Paid by cash
            public SoldItem[] items { get; set; }       // Items sold
        }

        public class SoldItem
        {
            public string product_name { get; set; }   // Item name
            public string quantity { get; set; }       // Quantity sold
            public string unit_price { get; set; }     // Price per unit
            public string subtotal { get; set; }       // Total price for this item
        }
        private void PrintBill(ApiSaleResponse sale)
        {
            PrintDocument pd = new PrintDocument();

            // Set 80mm paper size (3-inch width)
            pd.DefaultPageSettings.PaperSize = new PaperSize("Custom80mm", 300, 600);

            pd.PrintPage += (sender, args) =>
            {
                float yPos = 0;
                int leftMargin = 10;
                Font font = new Font("Courier New", 10);

                // Centered Shop Name (Bold)
                string shopName = "Mediflex Pharmacy";
                float printableWidthShop = args.MarginBounds.Width;

                SizeF shopNameSize = args.Graphics.MeasureString(shopName, new Font("Courier New", 12, FontStyle.Bold));
                float centerXshop = args.MarginBounds.Left + (printableWidthShop - shopNameSize.Width) / 2;
                args.Graphics.DrawString(shopName, new Font("Courier New", 12, FontStyle.Bold), Brushes.Black, centerXshop, yPos);
                yPos += 25;

                // Payment Method
                args.Graphics.DrawString($"Payment Method: {sale.payment_method}", font, Brushes.Black, leftMargin, yPos);
                yPos += 20;

                args.Graphics.DrawString(new string('-', 42), font, Brushes.Black, leftMargin, yPos);
                yPos += 15;

                // Header
                string header = string.Format("{0,-15} {1,6} {2,8} {3,8}", "Item", "Qty", "Price", "Total");
                args.Graphics.DrawString(header, font, Brushes.Black, leftMargin, yPos);
                yPos += 15;
                args.Graphics.DrawString(new string('-', 42), font, Brushes.Black, leftMargin, yPos);
                yPos += 15;

                // Items
                foreach (var item in sale.items)
                {
                    string line = string.Format("{0,-15} {1,6} {2,8} {3,8}",
                        item.product_name.Length > 15 ? item.product_name.Substring(0, 15) : item.product_name,
                        double.Parse(item.quantity).ToString("0.###"),
                        double.Parse(item.unit_price).ToString("0.00"),
                        double.Parse(item.subtotal).ToString("0.00"));
                    args.Graphics.DrawString(line, font, Brushes.Black, leftMargin, yPos);
                    yPos += 15;
                }

                args.Graphics.DrawString(new string('-', 42), font, Brushes.Black, leftMargin, yPos);
                yPos += 15;

                // Totals
                args.Graphics.DrawString($"Discount: {double.Parse(sale.discount).ToString("0.00")}", font, Brushes.Black, leftMargin, yPos);
                yPos += 15;

                double total = 0;
                foreach (var item in sale.items)
                    total += double.Parse(item.subtotal);

                args.Graphics.DrawString($"Total: {(total - double.Parse(sale.discount)).ToString("0.00")}", font, Brushes.Black, leftMargin, yPos);
                yPos += 15;

                if (double.Parse(sale.amount_paid) > 0)
                {
                    args.Graphics.DrawString($"Amount Paid: {double.Parse(sale.amount_paid).ToString("0.00")}", font, Brushes.Black, leftMargin, yPos);
                    yPos += 25;
                    double grand_total = total - double.Parse(sale.discount);
                    double balance = double.Parse(sale.amount_paid) - grand_total;
                    args.Graphics.DrawString($"Balance: {balance:0.00}", font, Brushes.Black, leftMargin, yPos);
                    yPos += 25;
                }

                args.Graphics.DrawString($"Printed By: {this.username}", font, Brushes.Black, leftMargin, yPos);
                yPos += 20;

                // Footer
                string thankYou = "Thank you for shopping!";
                float printableWidth = args.MarginBounds.Width;

                SizeF thankSize = args.Graphics.MeasureString(thankYou, font);
                float centerX = args.MarginBounds.Left + (printableWidth - thankSize.Width) / 2;

                args.Graphics.DrawString(thankYou, font, Brushes.Black, centerX, yPos);
            };

            pd.Print();
        }


        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // guard header or invalid clicks
            if (e.RowIndex < 0) return;

            // Action column is last (index 4 in your setup)
            if (e.ColumnIndex == 4)
            {
                try
                {
                    if (e.RowIndex >= 0 && e.RowIndex < items.Count)
                    {
                        items.RemoveAt(e.RowIndex); // will update bound grid automatically
                        UpdateGrandTotal();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("CellClick remove error: " + ex);
                }
            }
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            // protect against invalid row index
            if (e.RowIndex < 0) return;

            // Only respond to qty column changes
            if (e.ColumnIndex == 2)
            {
                UpdateTotal(e.RowIndex);
            }
        }

        private void dataGridView1_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            // safe guard
            if (dataGridView1.CurrentCell == null) return;
            int col = dataGridView1.CurrentCell.ColumnIndex;

            if (col != 2) return; // only attach for qty column

            // The editing control may or may not be a NumericUpDown depending on the custom column,
            // so only attach if it is a NumericUpDown
            if (e.Control is NumericUpDown nud)
            {
                // Avoid multiple subscriptions
                nud.ValueChanged -= Qty_ValueChanged;
                nud.ValueChanged += Qty_ValueChanged;
            }
        }

        private void Qty_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                if (dataGridView1.CurrentCell == null) return;

                int rowIndex = dataGridView1.CurrentCell.RowIndex;
                if (rowIndex < 0 || rowIndex >= items.Count) return;

                if (!(sender is NumericUpDown nud)) return;

                // update model from the control value
                items[rowIndex].qty = (int)nud.Value;

                // update computed total cell from model
                double price = items[rowIndex].selling_price;
                double qty = items[rowIndex].qty;

                // ensure the grid has that cell and it's writable
                var row = dataGridView1.Rows[rowIndex];
                if (row.Cells.Count > 3)
                    row.Cells[3].Value = "Rs. " + (price * qty).ToString("N2");

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Qty_ValueChanged error: " + ex);
            }
        }

        private void SetupTable()
        {
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.AllowUserToAddRows = false;

            dataGridView1.Columns.Clear();

            // Item name
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn()
            {
                DataPropertyName = "name",
                HeaderText = "Item Name",
                ReadOnly = true
            });

            // Price
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn()
            {
                DataPropertyName = "selling_price",
                HeaderText = "Price",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" },
                ReadOnly = true
            });

            // Qty -> Numeric Up/Down column (assumes DataGridViewNumericUpDownColumn exists)
            var qtyCol = new DataGridViewNumericUpDownColumn()
            {
                DataPropertyName = "qty",
                HeaderText = "Qty",
                Minimum = 1,
                Maximum = 9999,
                Increment = 1,
                Width = 80,
                ValueType = typeof(int) // important so Value is typed as int
            };
            dataGridView1.Columns.Add(qtyCol);

            // Total (computed)
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn()
            {
                HeaderText = "Total",
                ReadOnly = true
            });

            // Remove button
            DataGridViewButtonColumn btn = new DataGridViewButtonColumn();
            btn.HeaderText = "Action";
            btn.Text = "Remove";
            btn.UseColumnTextForButtonValue = true;
            btn.DefaultCellStyle.BackColor = Color.Red;
            btn.DefaultCellStyle.ForeColor = Color.White;
            dataGridView1.Columns.Add(btn);

            // wire handlers (only once)
            dataGridView1.CellClick -= dataGridView1_CellClick;
            dataGridView1.CellClick += dataGridView1_CellClick;

            dataGridView1.CellValueChanged -= dataGridView1_CellValueChanged;
            dataGridView1.CellValueChanged += dataGridView1_CellValueChanged;
        }

        private void dataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridView1.IsCurrentCellDirty)
            {
                dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void UpdateGrandTotal()
        {
            double grandTotal = items.Sum(i => i.selling_price * i.qty);
            label8.Text = "Rs. " + grandTotal.ToString("N2");
            UpdateDiscount();
        }
        private void UpdateDiscount()
        {
            double grandTotal = label8.Text.StartsWith("Rs. ") ?
                double.Parse(label8.Text.Substring(4)) : 0.0;

            // Get discount percentage
            double discountPercent = (double)numericUpDown1.Value;

            // Calculate discount amount
            double discountAmount = grandTotal * (discountPercent / 100);

            double balance = oldGrandTotal - (grandTotal - discountAmount);
            label2.Text = "Rs. " + balance.ToString("N2");

            // Update labels
            label9.Text = "Rs. " + discountAmount.ToString("N2");
            label1.Text = "Rs. " + (grandTotal - discountAmount).ToString("N2");
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            UpdateDiscount();
        }

        private List<ItemFromApi> FetchResultsFromApi(string sid)
        {
            try
            {
                string url = "https://ppos-318745238310.us-east1.run.app/api/sales/" + sid + "/";

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";

                // Add Authorization header (replace with real token)
                request.Headers.Add("Authorization", "Bearer " + this.accessToken);
                request.PreAuthenticate = true;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    ApiResponse apiResp = JsonConvert.DeserializeObject<ApiResponse>(json);
                    label24.Text = DateTime.Parse(apiResp.created_at).ToString("yyyy-MM-dd");
                    label6.Text = apiResp.payment_method;
                    double subtotal = Convert.ToDouble(apiResp.subtotal);
                    double discount = Convert.ToDouble(apiResp.discount);
                    double discount_precentage = (discount * 100) / subtotal;
                    numericUpDown1.Value = (decimal)discount_precentage;
                    label8.Text = "Rs. " + apiResp.subtotal;
                    label9.Text = "Rs. " + apiResp.discount;
                    label1.Text = "Rs. " + apiResp.total;
                    oldGrandTotal = Convert.ToDouble(apiResp.total);
                    return apiResp.items;
                }
            }
            catch
            {
                return null;
            }
        }

        private void UpdateTotal(int rowIndex)
        {
            try
            {
                if (rowIndex < 0) return;
                if (rowIndex >= items.Count) return;

                // compute from model (safer than reading cells)
                double price = items[rowIndex].selling_price;
                double qty = items[rowIndex].qty;

                double total = price * qty;

                // if grid row exists, update its total cell for display
                if (rowIndex < dataGridView1.Rows.Count)
                {
                    var row = dataGridView1.Rows[rowIndex];
                    if (row.Cells.Count > 3)
                        row.Cells[3].Value = "Rs. " + total.ToString("N2");
                }

                // update grand total
                UpdateGrandTotal();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateTotal error: " + ex);
            }
        }

        private void numericUpDown1_ValueChanged_1(object sender, EventArgs e)
        {
            UpdateGrandTotal();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private ApiSaleResponse CorrectSale(SaleRequest saleData, string sid)
        {
            try
            {
                string url = "https://ppos-318745238310.us-east1.run.app/api/sales/" + sid + "/correct_sale/" ;
                string jsonData = JsonConvert.SerializeObject(saleData);

                Console.WriteLine("Making sale with data: " + jsonData);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

                // Authorization header (Bearer token)
                request.Headers.Add("Authorization", "Bearer " + this.accessToken);
                request.PreAuthenticate = true;

                // Write JSON body
                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(jsonData);
                }

                // Read response
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string respJson = reader.ReadToEnd();
                    ApiSaleResponse saleResponse = JsonConvert.DeserializeObject<ApiSaleResponse>(respJson);
                    return saleResponse;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return null;
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            try
            {
                if (items == null || items.Count == 0)
                {
                    MessageBox.Show("Cart is empty. Add items before making a sale.", "No Items", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Build API-compatible items
                var saleItems = items
                    .Where(i => i.qty > 0)
                    .Select(i => new SaleItem
                    {
                        product = i.pid,                 // your product ID field
                        quantity = i.qty.ToString(),            // API expects string
                        unit_price = i.selling_price.ToString() // API expects string
                    })
                    .ToList();

                if (saleItems == null || saleItems.Count == 0)
                {
                    MessageBox.Show("No items with quantity > 0 found.", "No Items", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Parse total and discount
                double grandTotal = items.Sum(i => i.selling_price * i.qty);
                double discountPercent = (double)numericUpDown1.Value;
                double discountAmount = grandTotal * (discountPercent / 100.0);
                double netTotal = grandTotal - discountAmount;

                string discountString = discountAmount.ToString("0.00", CultureInfo.InvariantCulture);

                // Amount paid
                double amountPaidNum = (double)numericUpDown2.Value;
                string amountPaidString = amountPaidNum.ToString("0.00", CultureInfo.InvariantCulture);

                // Validate amount paid
                if (amountPaidNum < netTotal)
                {
                    var res = MessageBox.Show(
                        $"Amount paid ({amountPaidNum:N2}) is less than total ({netTotal:N2}). Continue?",
                        "Amount Paid Low",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );
                    if (res == DialogResult.No) return;
                }

                // Validate qty <= stock
                foreach (var it in items)
                {
                    if (it.qty <= 0)
                    {
                        MessageBox.Show($"Item '{it.name}' has invalid quantity {it.qty}.", "Invalid Quantity", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                // Build request according to new API
                var saleRequest = new SaleRequest
                {
                    correction_reason = "Normal Sale",   // You can make this dynamic
                    items = saleItems,
                    discount = discountString,
                    amount_paid = amountPaidString
                };

                Cursor prevCursor = Cursor.Current;
                try
                {
                    Cursor.Current = Cursors.WaitCursor;

                    var result = CorrectSale(saleRequest, billNumber);

                    if (result != null)
                    {
                        MessageBox.Show("Sale completed");
                        PrintBill(result);

                        items.Clear();
                        UpdateGrandTotal();
                        numericUpDown1.Value = 0;
                        numericUpDown2.Value = 0;
                    }
                    else
                    {
                        MessageBox.Show("Sale failed. Check server message.", "Sale Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                finally
                {
                    Cursor.Current = prevCursor;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("button7_Click error: " + ex);
                MessageBox.Show("Unexpected error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
