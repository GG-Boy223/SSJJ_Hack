using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Assets.Scripts.QuickRuntimeConsole;
using Assets.Scripts.QuickRuntimeConsole.commands;

namespace SkyDome.RuntimeConsole
{
    public class ExternalConsoleWindow : Form
    {
        private RichTextBox _outputTextBox;
        private TextBox _inputTextBox;
        private Label _locationLabel;
        private Button _executeButton;
        private Button _clearButton;
        private ListBox _suggestionListBox;
        private Panel _suggestionPanel;

        private List<string> _cmdHistory = new List<string>();
        private int _historyIndex;
        private string _location = "";

        private static readonly string[] _availableCommands = new string[]
        {
            "cd", "clear", "cmp", "code", "find", "invoke", "show", "var"
        };

        private Form _codeEditorForm;
        private RichTextBox _codeEditorTextBox;

        public ExternalConsoleWindow()
        {
            InitializeComponents();
            SetupEventHandlers();
        }

        private void InitializeComponents()
        {
            // 窗口设置
            this.Text = "Runtime Console";
            this.Size = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(25, 25, 25);
            this.MinimumSize = new Size(600, 400);

            // 输出文本框
            _outputTextBox = new RichTextBox
            {
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Both,
                WordWrap = false,
                BackColor = Color.FromArgb(15, 15, 15),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 10, FontStyle.Regular),
                Location = new Point(10, 40),
                Size = new Size(this.ClientSize.Width - 20, this.ClientSize.Height - 140),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Text = "=== Console Ready ===\n",
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(_outputTextBox);

            // 清空按钮
            _clearButton = new Button
            {
                Text = "Clear",
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft Sans Serif", 9, FontStyle.Regular),
                Location = new Point(10, 10),
                Size = new Size(60, 25),
                Cursor = Cursors.Hand
            };
            _clearButton.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 50);
            _clearButton.FlatAppearance.BorderSize = 1;
            this.Controls.Add(_clearButton);

            // 帮助按钮
            var helpButton = new Button
            {
                Text = "?",
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft Sans Serif", 9, FontStyle.Bold),
                Location = new Point(80, 10),
                Size = new Size(30, 25),
                Cursor = Cursors.Hand
            };
            helpButton.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 50);
            helpButton.FlatAppearance.BorderSize = 1;
            helpButton.Click += (s, e) =>
            {
                AppendOutput("Available commands: " + string.Join(", ", _availableCommands) + "\n");
                AppendOutput("Keys: Enter=Execute, Tab=Complete, Up/Down=History, Esc=Close\n");
            };
            this.Controls.Add(helpButton);

            // 位置标签
            _locationLabel = new Label
            {
                Text = ">",
                ForeColor = Color.FromArgb(100, 220, 100),
                Font = new Font("Consolas", 10, FontStyle.Bold),
                AutoSize = true,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            this.Controls.Add(_locationLabel);

            // 输入文本框
            _inputTextBox = new TextBox
            {
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 10, FontStyle.Regular),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(_inputTextBox);

            // 执行按钮
            _executeButton = new Button
            {
                Text = "Execute",
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft Sans Serif", 9, FontStyle.Bold),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            _executeButton.FlatAppearance.BorderColor = Color.FromArgb(50, 100, 150);
            _executeButton.FlatAppearance.BorderSize = 1;
            this.Controls.Add(_executeButton);

            // 建议面板 - 最后添加，确保在最上层
            _suggestionPanel = new Panel
            {
                BackColor = Color.FromArgb(45, 45, 45),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false,
                Size = new Size(this.ClientSize.Width - 20, 150),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            _suggestionListBox = new ListBox
            {
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 9, FontStyle.Regular),
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                ItemHeight = 18
            };
            _suggestionPanel.Controls.Add(_suggestionListBox);
            this.Controls.Add(_suggestionPanel);

            // 关键：将补全面板置于最前
            _suggestionPanel.BringToFront();

            UpdateLayout();
        }

        private void UpdateLayout()
        {
            int bottomY = this.ClientSize.Height - 70;

            // 位置标签显示在输入框上方
            _locationLabel.Location = new Point(10, bottomY - 20);

            // 输入框位置
            _inputTextBox.Location = new Point(10, bottomY + 5);
            _inputTextBox.Size = new Size(this.ClientSize.Width - 105, 25);

            // 执行按钮位置
            _executeButton.Location = new Point(this.ClientSize.Width - 85, bottomY + 5);
            _executeButton.Size = new Size(75, 25);

            // 建议面板位置（在输入框上方）
            _suggestionPanel.Location = new Point(10, bottomY - 175);
        }

        private void SetupEventHandlers()
        {
            this.Resize += (s, e) => UpdateLayout();

            _executeButton.Click += (s, e) => ExecuteCommand();

            _clearButton.Click += (s, e) =>
            {
                _outputTextBox.Clear();
                AppendOutput("=== Console Cleared ===\n");
            };

            _inputTextBox.KeyDown += InputTextBox_KeyDown;
            _inputTextBox.TextChanged += InputTextBox_TextChanged;

            _suggestionListBox.DoubleClick += (s, e) =>
            {
                if (_suggestionListBox.SelectedItem != null)
                {
                    ApplySuggestion(_suggestionListBox.SelectedItem.ToString());
                }
            };

            _suggestionListBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (_suggestionListBox.SelectedItem != null)
                    {
                        ApplySuggestion(_suggestionListBox.SelectedItem.ToString());
                    }
                    e.Handled = true;
                }
            };

            this.FormClosing += (s, e) =>
            {
                e.Cancel = true;
                this.Hide();
                ConsoleStatus.ConsoleOpened = false;
            };

            // 鼠标悬停效果
            _executeButton.MouseEnter += (s, e) => _executeButton.BackColor = Color.FromArgb(90, 150, 200);
            _executeButton.MouseLeave += (s, e) => _executeButton.BackColor = Color.FromArgb(70, 130, 180);
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (_suggestionPanel.Visible && _suggestionListBox.SelectedItem != null)
                {
                    ApplySuggestion(_suggestionListBox.SelectedItem.ToString());
                }
                else
                {
                    ExecuteCommand();
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Tab)
            {
                if (_suggestionPanel.Visible && _suggestionListBox.Items.Count > 0)
                {
                    if (_suggestionListBox.SelectedItem == null)
                    {
                        _suggestionListBox.SelectedIndex = 0;
                    }
                    if (_suggestionListBox.SelectedItem != null)
                    {
                        ApplySuggestion(_suggestionListBox.SelectedItem.ToString());
                    }
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (_suggestionPanel.Visible && _suggestionListBox.Items.Count > 0)
                {
                    int newIndex = Math.Max(0, _suggestionListBox.SelectedIndex - 1);
                    _suggestionListBox.SelectedIndex = newIndex;
                }
                else if (_cmdHistory.Count > 0)
                {
                    _historyIndex = Math.Max(0, _historyIndex - 1);
                    _inputTextBox.Text = _cmdHistory[_historyIndex];
                    _inputTextBox.SelectionStart = _inputTextBox.Text.Length;
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                if (_suggestionPanel.Visible && _suggestionListBox.Items.Count > 0)
                {
                    int newIndex = Math.Min(_suggestionListBox.Items.Count - 1, _suggestionListBox.SelectedIndex + 1);
                    _suggestionListBox.SelectedIndex = newIndex;
                }
                else if (_cmdHistory.Count > 0)
                {
                    _historyIndex = Math.Min(_cmdHistory.Count, _historyIndex + 1);
                    _inputTextBox.Text = _historyIndex < _cmdHistory.Count ? _cmdHistory[_historyIndex] : "";
                    _inputTextBox.SelectionStart = _inputTextBox.Text.Length;
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                if (_suggestionPanel.Visible)
                {
                    _suggestionPanel.Visible = false;
                }
                else
                {
                    this.Hide();
                    ConsoleStatus.ConsoleOpened = false;
                }
                e.Handled = true;
            }
        }

        private void InputTextBox_TextChanged(object sender, EventArgs e)
        {
            UpdateSuggestions();
        }

        private void UpdateSuggestions()
        {
            _suggestionListBox.Items.Clear();

            string input = _inputTextBox.Text;
            if (string.IsNullOrEmpty(input))
            {
                _suggestionPanel.Visible = false;
                return;
            }

            string[] parts = input.Split(' ');
            string lastPart = parts[parts.Length - 1];

            List<string> suggestions = new List<string>();

            if (parts.Length == 1)
            {
                foreach (string cmd in _availableCommands)
                {
                    if (cmd.StartsWith(lastPart, StringComparison.OrdinalIgnoreCase))
                    {
                        suggestions.Add(cmd);
                    }
                }
            }
            else if (ConsoleStatus.CurrObject != null && lastPart.Length > 0)
            {
                try
                {
                    var type = ConsoleStatus.CurrObject.GetType();

                    foreach (var prop in type.GetProperties())
                    {
                        if (prop.Name.StartsWith(lastPart, StringComparison.OrdinalIgnoreCase))
                        {
                            suggestions.Add($"{prop.Name} (prop)");
                        }
                    }

                    foreach (var field in type.GetFields())
                    {
                        if (field.Name.StartsWith(lastPart, StringComparison.OrdinalIgnoreCase))
                        {
                            suggestions.Add($"{field.Name} (field)");
                        }
                    }
                }
                catch { }
            }

            if (suggestions.Count > 0)
            {
                _suggestionListBox.Items.AddRange(suggestions.Take(10).ToArray());
                _suggestionListBox.SelectedIndex = 0;
                _suggestionPanel.Visible = true;
                _suggestionPanel.BringToFront();  // 确保显示在最前面
            }
            else
            {
                _suggestionPanel.Visible = false;
            }
        }

        private void ApplySuggestion(string suggestion)
        {
            if (string.IsNullOrEmpty(suggestion)) return;

            // 移除类型标记
            suggestion = suggestion.Split(' ')[0];

            string[] parts = _inputTextBox.Text.Split(' ');
            parts[parts.Length - 1] = suggestion;
            _inputTextBox.Text = string.Join(" ", parts);
            _inputTextBox.SelectionStart = _inputTextBox.Text.Length;
            _suggestionPanel.Visible = false;
            _inputTextBox.Focus();
        }

        private void ExecuteCommand()
        {
            string input = _inputTextBox.Text.Trim();
            _inputTextBox.Clear();
            _suggestionPanel.Visible = false;

            if (!string.IsNullOrEmpty(input))
            {
                _cmdHistory.Remove(input);
                _cmdHistory.Add(input);
                _historyIndex = _cmdHistory.Count;
            }

            string output = _outputTextBox.Text;
            Execute(input, ref output, ref _location);

            _outputTextBox.Text = output;
            _outputTextBox.SelectionStart = _outputTextBox.Text.Length;
            _outputTextBox.ScrollToCaret();

            _locationLabel.Text = _location + ">";
            _inputTextBox.Focus();
        }

        private void AppendOutput(string text)
        {
            _outputTextBox.AppendText(text);
            _outputTextBox.ScrollToCaret();
        }

        private void Execute(string str, ref string output, ref string location)
        {
            string[] lines = output.Split('\n');
            if (lines.Length > 500)
            {
                output = string.Join("\n", lines.Skip(lines.Length - 400).ToArray());
            }

            if (!string.IsNullOrEmpty(str))
            {
                output += $"{location}> {str}\n";
            }

            if (string.IsNullOrEmpty(str))
            {
                output += $"{location}>\n";
                return;
            }

            string[] args = str.Split(' ');

            try
            {
                if (ConsoleStatus.LockCommand != null)
                {
                    ConsoleStatus.LockCommand.Execute(args, ref output, ref location);

                    if (ConsoleStatus.ShowEditView && _codeEditorForm == null)
                    {
                        ShowCodeEditor();
                    }
                }
                else
                {
                    IRuntimeCommand command = CommandFactory.GetCommand(args[0]);
                    if (command != null)
                    {
                        command.Execute(args.Skip(1).ToArray(), ref output, ref location);

                        if (ConsoleStatus.ShowEditView && _codeEditorForm == null)
                        {
                            ShowCodeEditor();
                        }
                    }
                    else
                    {
                        output += $"[ERROR] Unknown command: {args[0]}\n";
                    }
                }
            }
            catch (Exception ex)
            {
                output += $"[ERROR] {ex.Message}\n";
            }
        }


        private void ShowCodeEditor()
        {
            if (_codeEditorForm != null && !_codeEditorForm.IsDisposed)
            {
                _codeEditorForm.BringToFront();
                return;
            }

            _codeEditorForm = new Form
            {
                Text = "Code Editor",
                Size = new Size(900, 700),
                StartPosition = FormStartPosition.CenterParent,
                TopMost = true,
                BackColor = Color.FromArgb(25, 25, 25),
                MinimumSize = new Size(600, 400)
            };

            _codeEditorTextBox = new RichTextBox
            {
                ScrollBars = RichTextBoxScrollBars.Both,
                WordWrap = false,
                BackColor = Color.FromArgb(15, 15, 15),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 10, FontStyle.Regular),
                Location = new Point(10, 10),
                Size = new Size(_codeEditorForm.ClientSize.Width - 20, _codeEditorForm.ClientSize.Height - 60),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Text = ConsoleStatus.EditText,
                BorderStyle = BorderStyle.FixedSingle
            };
            _codeEditorForm.Controls.Add(_codeEditorTextBox);

            var doneButton = new Button
            {
                Text = "✓ Done",
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft Sans Serif", 9, FontStyle.Bold),
                Location = new Point(10, _codeEditorForm.ClientSize.Height - 40),
                Size = new Size(90, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Cursor = Cursors.Hand
            };
            doneButton.FlatAppearance.BorderColor = Color.FromArgb(50, 100, 150);
            doneButton.Click += (s, e) =>
            {
                ConsoleStatus.EditText = _codeEditorTextBox.Text;
                string output = _outputTextBox.Text;
                Execute("code end", ref output, ref _location);
                _outputTextBox.Text = output;
                _locationLabel.Text = _location + ">";
                _codeEditorForm.Close();
            };
            _codeEditorForm.Controls.Add(doneButton);

            var cancelButton = new Button
            {
                Text = "✕ Cancel",
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft Sans Serif", 9, FontStyle.Regular),
                Location = new Point(110, _codeEditorForm.ClientSize.Height - 40),
                Size = new Size(90, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Cursor = Cursors.Hand
            };
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 50);
            cancelButton.Click += (s, e) =>
            {
                ConsoleStatus.ShowEditView = false;
                ConsoleStatus.LockCommand = null;
                _codeEditorForm.Close();
            };
            _codeEditorForm.Controls.Add(cancelButton);

            _codeEditorForm.FormClosed += (s, e) =>
            {
                _codeEditorForm = null;
                ConsoleStatus.ShowEditView = false;
            };

            _codeEditorForm.Show();
            _codeEditorTextBox.Focus();
        }

        public void ShowConsole()
        {
            this.Show();
            this.BringToFront();
            _inputTextBox.Focus();
        }
    }
}
