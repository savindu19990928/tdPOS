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
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolBar;

namespace PposSystem
{
    public partial class sale_system : Form
    {
        private string accessToken;
        private string username;
        public sale_system(string accessToken, string username)
        {
            InitializeComponent();
            this.accessToken = accessToken;
            this.username = username;
        }

        public class Item
        {
            public int id { get; set; }
            public string name { get; set; }
            public string barcode { get; set; }
            public string barcode_image { get; set; }
            public double selling_price { get; set; }
            public int unit_of_measure { get; set; }
            public string unit_of_measure_name { get; set; }
            public string unit_abbreviation { get; set; }
            public double current_stock { get; set; }

            // changed to int to match NumericUpDown value type
            public double qty { get; set; }
        }

        public class ApiResponse
        {
            public int count { get; set; }
            public string next { get; set; }
            public string previous { get; set; }
            public List<ItemFromApi> results { get; set; }
        }

        public class ItemFromApi
        {
            public int id { get; set; }
            public string name { get; set; }
            public double selling_price { get; set; }
            public string barcode { get; set; }
            public string barcode_image { get; set; }
            public int unit_of_measure { get; set; }
            public string unit_of_measure_name { get; set; }
            public string unit_abbreviation { get; set; }
            public double current_stock { get; set; }
        }

        public class SaleRequest
        {
            public int cash_register { get; set; } = 1;  // Always 1

            [JsonConverter(typeof(StringEnumConverter))]
            public PaymentMethod payment_method { get; set; }

            public string discount { get; set; }
            public string amount_paid { get; set; }
            public string customerName { get; set; }
            public string customerMobile { get; set; }
            public List<SaleItem> items { get; set; }
        }

        public enum PaymentMethod
        {
            CASH,
            CARD,
            MIXED
        }

        public class SaleItem
        {
            public int product { get; set; }
            public string quantity { get; set; }
            public string unit_price { get; set; }
        }


        // Use BindingList + BindingSource so we don't need to rebind DataSource repeatedly
        BindingList<Item> items = new BindingList<Item>();
        BindingSource itemsBindingSource;

        private System.Windows.Forms.Timer debounceTimer;

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

        // Change PrintBill from static to instance method so it can access 'username'
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

        private ApiSaleResponse MakeSale(SaleRequest saleData)
        {
            try
            {
                string url = "https://ppos-318745238310.us-east1.run.app/api/sales/";
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

        private void LoadSearchResultsAsync(string query)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                List<ItemFromApi> results = FetchResultsFromApi(query);

                this.Invoke((MethodInvoker)delegate
                {
                    if (results != null && results.Count > 0)
                    {
                        lstResults.DataSource = null;
                        lstResults.Items.Clear();
                        lstResults.DataSource = results;
                        lstResults.DisplayMember = "name";
                        lstResults.ValueMember = "id";
                    }
                    else
                    {
                        lstResults.DataSource = null;
                        lstResults.Items.Clear();
                    }
                });
            });
        }

        private void LstResults_Click(object sender, EventArgs e)
        {
            if (lstResults.SelectedIndex < 0 || lstResults.SelectedItem == null) return;

            ItemFromApi selected = (ItemFromApi)lstResults.SelectedItem;

            AddItem(
                selected.id,
                selected.name,
                selected.selling_price,
                selected.barcode,
                selected.barcode_image,
                selected.unit_of_measure,
                selected.unit_of_measure_name,
                selected.unit_abbreviation,
                selected.current_stock,
                1
            );
            txtSearch.Text = "";
            //debounceTimer.Stop();
            lstResults.DataSource = null;
            lstResults.Items.Clear();
        }

        private List<ItemFromApi> FetchResultsFromApi(string query)
        {
            try
            {
                string url = "https://ppos-318745238310.us-east1.run.app/api/products/?search=" + Uri.EscapeDataString(query);

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
                    return apiResp.results;
                }
            }
            catch
            {
                return null;
            }
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            lstResults.DataSource = null;
            lstResults.Items.Clear();
            if (txtSearch.Text.Length < 3)
            {
                return;
            }
            if (txtSearch.Text == "")
            {
                //debounceTimer.Stop();
                return;
            }
            List<ItemFromApi> results = FetchResultsFromApi(txtSearch.Text);

            if (results != null && results.Count > 0)
            {
                lstResults.DataSource = null;
                lstResults.Items.Clear();
                lstResults.DataSource = results;
                lstResults.DisplayMember = "name";
                lstResults.ValueMember = "id";
            }
            else
            {
                lstResults.DataSource = null;
                lstResults.Items.Clear();
            }
        }

        private void DebounceTimer_Tick(object sender, EventArgs e)
        {
            debounceTimer.Stop();
            LoadSearchResultsAsync(txtSearch.Text);
        }

        private void sale_system_Load(object sender, EventArgs e)
        {
            SetupTable();
            comboBox1.SelectedIndex = 0;
            comboBox2.SelectedIndex = 0;

            // Use BindingSource so grid updates automatically with list changes
            itemsBindingSource = new BindingSource();
            itemsBindingSource.DataSource = items;
            dataGridView1.DataSource = itemsBindingSource;

            // Hook events
            dataGridView1.EditingControlShowing += dataGridView1_EditingControlShowing;
            dataGridView1.CurrentCellDirtyStateChanged += dataGridView1_CurrentCellDirtyStateChanged;

            lstResults.Click += LstResults_Click;

            // 300ms debounce timer
            //debounceTimer = new System.Windows.Forms.Timer();
            //debounceTimer.Interval = 300;
            //debounceTimer.Tick += DebounceTimer_Tick;
        }

        private void AddItem(
            int id,
            string name,
            double selling_price,
            string barcode = "",
            string barcode_image = "",
            int unit_of_measure = 0,
            string unit_of_measure_name = "",
            string unit_abbreviation = "",
            double current_stock = 0,
            int qty = 1)
        {
            // 🔥 CHECK FOR DUPLICATES
            var existing = items.FirstOrDefault(i => i.id == id);
            if (existing != null)
            {
                // Instead of adding a new row, just increase Qty
                existing.qty += qty;

                // Update totals
                int rowIndex = items.IndexOf(existing);
                UpdateTotal(rowIndex);
                return;
            }

            // No duplicates → Add new item
            items.Add(new Item
            {
                id = id,
                name = name,
                barcode = barcode,
                barcode_image = barcode_image,
                selling_price = selling_price,
                unit_of_measure = unit_of_measure,
                unit_of_measure_name = unit_of_measure_name,
                unit_abbreviation = unit_abbreviation,
                current_stock = current_stock,
                qty = qty
            });
            UpdateGrandTotal();
        }

        private List<SaleItem> BuildSaleItems(List<Item> sourceItems)
        {
            return sourceItems
                .Where(i => i.qty > 0)
                .Select(i => new SaleItem
                {
                    product = i.id,
                    quantity = Convert.ToInt32(i.qty).ToString(), // keep as string per API
                    unit_price = i.selling_price.ToString(CultureInfo.InvariantCulture)
                })
                .ToList();
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

                // Build sale items (only include qty > 0)
                var saleItems = BuildSaleItems(items.ToList());
                if (saleItems == null || saleItems.Count == 0)
                {
                    MessageBox.Show("No items with quantity > 0 found.", "No Items", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Get payment method from comboBoxPayment (if present) otherwise default to CASH
                PaymentMethod paymentMethod = PaymentMethod.CASH;

                try
                {
                    string selected = comboBox1.SelectedItem?.ToString(); // safe null check
                    if (selected == "CARD")
                    {
                        paymentMethod = PaymentMethod.CARD;
                    }
                    else if (selected == "MIXED")
                    {
                        paymentMethod = PaymentMethod.MIXED;
                    }
                }
                catch
                {
                    // ignore
                }

                // Amount paid from txtAmountPaid (if present)
                double num2 = (double)numericUpDown2.Value;

                string lbl12 = label12.Text.Replace("Rs.", "").Trim();
                double lbl12Val = 0;
                double.TryParse(lbl12, out lbl12Val);
                string amountPaidStr = (num2 - lbl12Val).ToString();
                double amountPaid = (double)numericUpDown2.Value;

                if (paymentMethod == PaymentMethod.CARD)
                {
                    amountPaid = 0;
                }

                // Calculate expected total (after discount)
                double grandTotal = items.Sum(i => i.selling_price * i.qty);
                double discountPercent = (double)numericUpDown1.Value;
                double discountAmount = grandTotal * (discountPercent / 100.0);
                double netTotal = grandTotal - discountAmount;

                // Discount percent from numericUpDown1
                string discountStr = ((double)discountAmount).ToString();

                // Basic validation: amountPaid should be >= netTotal for CASH. For MIXED we still accept user input.
                if ((paymentMethod == PaymentMethod.CASH) && amountPaid < netTotal)
                {
                    var res = MessageBox.Show($"Amount paid ({amountPaid.ToString("N2")}) is less than total ({netTotal.ToString("N2")}). Continue anyway?", "Amount Paid Low", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
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
                    if (it.current_stock > -1 && it.qty > it.current_stock)
                    {
                        var r = MessageBox.Show($"Quantity for '{it.name}' exceeds current stock ({it.current_stock}). Continue?", "Stock Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (r == DialogResult.No) return;
                    }
                }

                // Build request
                var saleRequest = new SaleRequest
                {
                    payment_method = paymentMethod,
                    discount = discountStr,
                    amount_paid = amountPaid.ToString(CultureInfo.InvariantCulture),
                    items = saleItems
                };

                // Show a simple wait cursor while the network call happens
                Cursor previousCursor = Cursor.Current;
                try
                {
                    Cursor.Current = Cursors.WaitCursor;

                    // Call MakeSale (network)
                    var result = MakeSale(saleRequest);

                    if (result != null)
                    {
                        MessageBox.Show("Sale completed");

                        PrintBill(result);

                        // Clear cart and reset UI
                        items.Clear();
                        UpdateGrandTotal();
                        numericUpDown1.Value = 0;
                        // clear amount paid textbox if exists
                        numericUpDown2.Value = 0;
                    }
                    else
                    {
                        MessageBox.Show("Sale failed. See logs or server message for details.", "Sale Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                finally
                {
                    Cursor.Current = previousCursor;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("button7_Click error: " + ex);
                MessageBox.Show("Unexpected error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateDiscount()
        {
            double grandTotal = label8.Text.StartsWith("Rs. ") ?
                double.Parse(label8.Text.Substring(4)) : 0.0;

            // Get discount percentage
            double discountPercent = (double)numericUpDown1.Value;

            // Calculate discount amount
            double discountAmount = grandTotal * (discountPercent / 100);

            // Update labels
            label9.Text = "Rs. " + discountAmount.ToString("N2");
            label11.Text = "Rs. " + (grandTotal - discountAmount).ToString("N2");
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            UpdateDiscount();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            txtSearch.Text = "";
            //debounceTimer.Stop();
            lstResults.DataSource = null;
            items.Clear();
            numericUpDown1.Value = 0;
            UpdateGrandTotal();
        }

        private void button13_Click(object sender, EventArgs e)
        {
            txtSearch.Focus();
        }

        private void sale_system_FormClosed(object sender, FormClosedEventArgs e)
        {
            Application.Exit();
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedText == "CARD" || comboBox1.SelectedText == "MIXED")
            {
                label12.Text = "Rs. 0.00";
                return;
            }
            double num2 = (double)numericUpDown2.Value;

            string lbl11 = label11.Text.Replace("Rs.", "").Trim();
            double lbl11Val = 0;

            double.TryParse(lbl11, out lbl11Val);

            label12.Text = "Rs. " + (num2 - lbl11Val).ToString("0.00");
        }

        private void dataGridView1_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            UpdateTotal(e.RowIndex);
        }

        private void btnBillSearch_Click(object sender, EventArgs e)
        {
            string sid = txtBillSearch.Text.Trim();

            if (string.IsNullOrEmpty(sid))
            {
                MessageBox.Show("Please enter a bill number.");
                return;
            }

            try
            {
                string url = "https://ppos-318745238310.us-east1.run.app/api/sales/" + sid + "/";

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";

                // Add Authorization header
                request.Headers.Add("Authorization", "Bearer " + this.accessToken);
                request.PreAuthenticate = true;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        // Bill exists → open form
                        BillEdit billEditForm = new BillEdit(accessToken, sid, username);
                        billEditForm.Show();
                    }
                    else
                    {
                        MessageBox.Show("Unexpected response: " + response.StatusCode);
                    }
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse errorResponse = ex.Response as HttpWebResponse;

                if (errorResponse != null && errorResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    MessageBox.Show("Bill number not found.");
                }
                else
                {
                    MessageBox.Show("Network or server error: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void txtBillSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnBillSearch.PerformClick();
            }
        }
    }
}
