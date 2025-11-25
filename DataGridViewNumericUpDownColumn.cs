using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PposSystem
{

    public class DataGridViewNumericUpDownColumn : DataGridViewColumn
    {
        public DataGridViewNumericUpDownColumn() : base(new DataGridViewNumericUpDownCell())
        {
        }

        public int Minimum { get; set; } = 0;
        public int Maximum { get; set; } = 9999;
        public int Increment { get; set; } = 1;

        public override object Clone()
        {
            var col = (DataGridViewNumericUpDownColumn)base.Clone();
            col.Minimum = Minimum;
            col.Maximum = Maximum;
            col.Increment = Increment;
            return col;
        }
    }

    public class DataGridViewNumericUpDownCell : DataGridViewTextBoxCell
    {
        public override void InitializeEditingControl(int rowIndex, object initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
        {
            base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);

            NumericUpDown ctl = (NumericUpDown)DataGridView.EditingControl;

            if (Value != null)
                ctl.Value = Convert.ToDecimal(Value);
        }

        public override Type EditType => typeof(DataGridViewNumericUpDownEditingControl);
        public override Type ValueType => typeof(int);
        public override object DefaultNewRowValue => 1;
    }

    public class DataGridViewNumericUpDownEditingControl : NumericUpDown, IDataGridViewEditingControl
    {
        DataGridView dataGridView;
        private bool valueChanged = false;
        int rowIndex;

        public DataGridViewNumericUpDownEditingControl()
        {
            Minimum = 0;
            Maximum = 9999;
        }

        public object EditingControlFormattedValue
        {
            get => Value.ToString();
            set
            {
                if (value is string s && int.TryParse(s, out int v))
                    Value = v;
            }
        }

        public object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context) => EditingControlFormattedValue;
        public void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle) { }
        public int EditingControlRowIndex { get => rowIndex; set => rowIndex = value; }
        public bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey) => true;
        public void PrepareEditingControlForEdit(bool selectAll) { }
        public bool RepositionEditingControlOnValueChange => false;
        public DataGridView EditingControlDataGridView { get => dataGridView; set => dataGridView = value; }
        public bool EditingControlValueChanged { get => valueChanged; set => valueChanged = value; }
        public Cursor EditingPanelCursor => base.Cursor;

        protected override void OnValueChanged(EventArgs e)
        {
            valueChanged = true;
            EditingControlDataGridView.NotifyCurrentCellDirty(true);
            base.OnValueChanged(e);
        }
    }

}
