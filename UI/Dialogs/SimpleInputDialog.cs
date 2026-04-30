using System;
using System.Drawing;
using System.Windows.Forms;

namespace AppTimeTracker.UI.Dialogs
{
    public partial class SimpleInputDialog : Form
    {
        private Label labelCurrent;
        private TextBox textBox;
        private Button buttonOK;
        private Button buttonCancel;

        // Цвета из DarkTheme.xaml
        private readonly Color _backgroundColor = Color.FromArgb(0xFF, 0x0A, 0x0A, 0x0A);      // #FF0A0A0A
        private readonly Color _textColor = Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0);          // #FFF0F0F0
        private readonly Color _textboxBackground = Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A);  // #FF1A1A1A
        private readonly Color _borderColor = Color.FromArgb(0xFF, 0x33, 0x33, 0x33);        // #FF333333
        private readonly Color _accentColor = Color.FromArgb(0xFF, 0x9B, 0x00, 0x00);        // #FF9B0000
        private readonly Color _accentColorDark = Color.FromArgb(0xFF, 0x7A, 0x00, 0x00);    // #FF7A0000
        private readonly Color _accentColorLight = Color.FromArgb(0xFF, 0xBC, 0x00, 0x00);   // #FFBC0000

        public string EnteredText => textBox.Text;

        public SimpleInputDialog(string currentName, string title = "Введите текст")
        {
            InitializeComponent();
            this.Text = title;
            labelCurrent.Text = $"Текущее: {currentName}";
            textBox.Text = currentName;
            textBox.SelectAll();

            // Убираем системную рамку
            this.FormBorderStyle = FormBorderStyle.None;

            // Стилизация формы
            ApplyDarkTheme();
        }

        private void InitializeComponent()
        {
            this.labelCurrent = new Label();
            this.textBox = new TextBox();
            this.buttonOK = new Button();
            this.buttonCancel = new Button();
            this.SuspendLayout();

            // Label
            this.labelCurrent.Location = new Point(12, 15);
            this.labelCurrent.Name = "labelCurrent";
            this.labelCurrent.Size = new Size(360, 20);
            this.labelCurrent.TabIndex = 0;
            this.labelCurrent.Text = "Текущее:";

            // TextBox
            this.textBox.Location = new Point(12, 40);
            this.textBox.Name = "textBox";
            this.textBox.Size = new Size(360, 23);
            this.textBox.TabIndex = 1;

            // Кнопка OK
            this.buttonOK.Location = new Point(216, 75);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new Size(75, 23);
            this.buttonOK.TabIndex = 2;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.DialogResult = DialogResult.OK;

            // Кнопка Cancel
            this.buttonCancel.Location = new Point(297, 75);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new Size(75, 23);
            this.buttonCancel.TabIndex = 3;
            this.buttonCancel.Text = "Отмена";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.DialogResult = DialogResult.Cancel;

            this.AcceptButton = this.buttonOK;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new Size(384, 110);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.textBox);
            this.Controls.Add(this.labelCurrent);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void ApplyDarkTheme()
        {
            // Форма
            this.BackColor = _backgroundColor;
            this.ForeColor = _textColor;

            // Кастомная рамка (имитируем системную темную)
            this.Paint += (s, e) =>
            {
                e.Graphics.DrawRectangle(new Pen(_borderColor, 1),
                    0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
            };

            // Label
            labelCurrent.ForeColor = _textColor;
            labelCurrent.BackColor = _backgroundColor;

            // TextBox
            textBox.BackColor = _textboxBackground;
            textBox.ForeColor = _textColor;
            textBox.BorderStyle = BorderStyle.FixedSingle;

            // Убираем стандартный вид кнопок
            buttonOK.FlatStyle = FlatStyle.Flat;
            buttonOK.BackColor = _accentColor;
            buttonOK.ForeColor = Color.White;
            buttonOK.FlatAppearance.BorderColor = _accentColor;
            buttonOK.FlatAppearance.BorderSize = 1;

            buttonCancel.FlatStyle = FlatStyle.Flat;
            buttonCancel.BackColor = _textboxBackground;
            buttonCancel.ForeColor = _textColor;
            buttonCancel.FlatAppearance.BorderColor = _borderColor;
            buttonCancel.FlatAppearance.BorderSize = 1;

            // Hover эффекты для OK
            buttonOK.MouseEnter += (s, e) =>
            {
                buttonOK.BackColor = _accentColorLight;
                buttonOK.FlatAppearance.BorderColor = _accentColorLight;
            };
            buttonOK.MouseLeave += (s, e) =>
            {
                buttonOK.BackColor = _accentColor;
                buttonOK.FlatAppearance.BorderColor = _accentColor;
            };
            buttonOK.MouseDown += (s, e) =>
            {
                buttonOK.BackColor = _accentColorDark;
                buttonOK.FlatAppearance.BorderColor = _accentColorDark;
            };

            // Hover эффекты для Cancel
            buttonCancel.MouseEnter += (s, e) =>
            {
                buttonCancel.BackColor = Color.FromArgb(0xFF, 0x2A, 0x2A, 0x2A);
                buttonCancel.FlatAppearance.BorderColor = _accentColor;
            };
            buttonCancel.MouseLeave += (s, e) =>
            {
                buttonCancel.BackColor = _textboxBackground;
                buttonCancel.FlatAppearance.BorderColor = _borderColor;
            };
            buttonCancel.MouseDown += (s, e) =>
            {
                buttonCancel.BackColor = Color.FromArgb(0xFF, 0x44, 0x44, 0x44);
                buttonCancel.FlatAppearance.BorderColor = _accentColorDark;
            };

            // Обработка Escape
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }
            };
        }
    }
}