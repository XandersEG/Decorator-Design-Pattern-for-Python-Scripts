using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IDE_Decorator.Modelo;

namespace IDE_Decorator
{
    public partial class IDE : Window
    {
        private bool isModified = false;
        private Process currentPythonProcess;
        private Process terminalProcess;
        private bool running;
        private string projectName;
        private string currentProjectPath;
        private string currentFilePath;

        private string sysCurrentPath;
        private bool sysFilterPyOnly = true;

        private enum ActiveTab { Project, System, Tasks }
        private ActiveTab _activeTab = ActiveTab.Project;

        public IDE()
        {
            InitializeComponent();

            this.running = false;
            this.projectName = "Proyecto Python Autónomo";

            txtEditor.AddHandler(ScrollViewer.ScrollChangedEvent,
                new ScrollChangedEventHandler(txtEditor_ScrollChanged));
            ActualizarNumerosLinea();

            btnStop.IsEnabled = false;
            btnStop.Visibility = Visibility.Collapsed;
            txtConsole.Visibility = Visibility.Collapsed;
            txtConsoleSeparator.Visibility = Visibility.Collapsed;
            spConsoleInput.Visibility = Visibility.Collapsed;

            lblProjectName.Content = this.projectName;
            this.Topmost = true;

            var enunciados = new List<Assignment>
            {
                new Assignment { Id = 101, Title = "Tarea 1",
                    Deadline = DateTime.Now.AddDays(7),
                    Description = "Desarrolle un script básico en Python para familiarizarse con el entorno." },
                new Assignment { Id = 33, Title = "Tarea 2",
                    Deadline = DateTime.Now.AddDays(14),
                    Description = "Serie Fibonacci:\nCree un programa que calcule la serie de Fibonacci utilizando recursividad o ciclos." }
            };
            icTareas.ItemsSource = enunciados;

            string defaultRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "IDEPythonProjects");
            if (!Directory.Exists(defaultRoot)) Directory.CreateDirectory(defaultRoot);
            LoadProject(defaultRoot);

            sysCurrentPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (string.IsNullOrEmpty(sysCurrentPath) || !Directory.Exists(sysCurrentPath))
                sysCurrentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            LoadSystemTree(sysCurrentPath);
        }

        private void btnTabProject_Click(object sender, RoutedEventArgs e) => SwitchTab(ActiveTab.Project);
        private void btnTabSystem_Click(object sender, RoutedEventArgs e) => SwitchTab(ActiveTab.System);
        private void btnTabTasks_Click(object sender, RoutedEventArgs e) => SwitchTab(ActiveTab.Tasks);

        private void SwitchTab(ActiveTab tab)
        {
            _activeTab = tab;

            tabProject.Visibility = tab == ActiveTab.Project ? Visibility.Visible : Visibility.Collapsed;
            tabSystem.Visibility = tab == ActiveTab.System ? Visibility.Visible : Visibility.Collapsed;
            tabTasks.Visibility = tab == ActiveTab.Tasks ? Visibility.Visible : Visibility.Collapsed;

            btnTabProject.Background = tab == ActiveTab.Project
                ? new SolidColorBrush(Color.FromRgb(63, 65, 133)) : Brushes.Transparent;
            btnTabProject.Foreground = tab == ActiveTab.Project
                ? Brushes.White : new SolidColorBrush(Color.FromRgb(170, 170, 170));

            btnTabSystem.Background = tab == ActiveTab.System
                ? new SolidColorBrush(Color.FromRgb(63, 65, 133)) : Brushes.Transparent;
            btnTabSystem.Foreground = tab == ActiveTab.System
                ? Brushes.White : new SolidColorBrush(Color.FromRgb(170, 170, 170));

            btnTabTasks.Background = tab == ActiveTab.Tasks
                ? new SolidColorBrush(Color.FromRgb(63, 65, 133)) : Brushes.Transparent;
            btnTabTasks.Foreground = tab == ActiveTab.Tasks
                ? Brushes.White : new SolidColorBrush(Color.FromRgb(170, 170, 170));

            if (tab == ActiveTab.System && tvSystem.Items.Count == 0)
                LoadSystemTree(sysCurrentPath);
        }

        private void LoadSystemTree(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) return;
            sysCurrentPath = rootPath;
            txtSysPath.Text = rootPath;
            tvSystem.Items.Clear();

            var root = BuildSystemNode(rootPath, isRoot: true);
            if (root != null)
            {
                tvSystem.Items.Add(root);
                root.IsExpanded = true;
            }
        }

        private TreeViewItem BuildSystemNode(string path, bool isRoot = false)
        {
            try
            {
                string label = isRoot
                    ? Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) + $" ({path})"
                    : Path.GetFileName(path);

                var node = new TreeViewItem
                {
                    Header = "📁 " + label,
                    Tag = path,
                    Foreground = new SolidColorBrush(Color.FromRgb(135, 206, 235))
                };

                if (HasChildren(path))
                    node.Items.Add(new TreeViewItem { Header = "Cargando...", Tag = "__placeholder__" });

                return node;
            }
            catch { return null; }
        }

        private bool HasChildren(string path)
        {
            try
            {
                foreach (var _ in Directory.EnumerateDirectories(path)) return true;
                string pattern = sysFilterPyOnly ? "*.py" : "*.*";
                foreach (var _ in Directory.EnumerateFiles(path, pattern)) return true;
            }
            catch { }
            return false;
        }

        private void tvSystem_Expanded(object sender, RoutedEventArgs e)
        {
            if (!(e.OriginalSource is TreeViewItem node)) return;
            if (!(node.Tag is string dirPath) || !Directory.Exists(dirPath)) return;

            if (node.Items.Count == 1 &&
                node.Items[0] is TreeViewItem ph &&
                ph.Tag is string tag && tag == "__placeholder__")
            {
                node.Items.Clear();
                PopulateSystemNode(node, dirPath);
            }
        }

        private void PopulateSystemNode(TreeViewItem parent, string dirPath)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(dirPath))
                {
                    var child = BuildSystemNode(dir);
                    if (child != null) parent.Items.Add(child);
                }

                string pattern = sysFilterPyOnly ? "*.py" : "*.*";
                foreach (var file in Directory.GetFiles(dirPath, pattern))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    string icon = ext == ".py" ? "🐍 " : "📄 ";
                    var fileNode = new TreeViewItem
                    {
                        Header = icon + Path.GetFileName(file),
                        Tag = file,
                        Foreground = ext == ".py"
                            ? new SolidColorBrush(Color.FromRgb(243, 221, 78))
                            : new SolidColorBrush(Color.FromRgb(204, 204, 204))
                    };
                    parent.Items.Add(fileNode);
                }
            }
            catch { }
        }

        private void tvSystem_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) { }

        private void tvSystem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(tvSystem.SelectedItem is TreeViewItem node)) return;
            if (!(node.Tag is string path)) return;

            if (File.Exists(path))
            {
                OpenFileInEditor(path);
            }
            else if (Directory.Exists(path))
            {
                sysCurrentPath = path;
                txtSysPath.Text = path;

                if (node.Items.Count == 1 &&
                    node.Items[0] is TreeViewItem ph && ph.Tag is string t && t == "__placeholder__")
                {
                    node.Items.Clear();
                    PopulateSystemNode(node, path);
                }
                node.IsExpanded = !node.IsExpanded;
            }
        }

        private void btnSysUp_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(sysCurrentPath)) return;
            string parent = Directory.GetParent(sysCurrentPath)?.FullName;
            if (!string.IsNullOrEmpty(parent)) LoadSystemTree(parent);
        }

        private void btnSysGo_Click(object sender, RoutedEventArgs e)
        {
            string path = txtSysPath.Text.Trim();
            if (Directory.Exists(path)) LoadSystemTree(path);
            else MessageBox.Show("La ruta no existe o no es un directorio.", "Ruta inválida",
                                 MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void txtSysPath_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) btnSysGo_Click(sender, e);
        }

        private void cmbSysFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            sysFilterPyOnly = (cmbSysFilter.SelectedIndex == 0);
            if (!string.IsNullOrEmpty(sysCurrentPath)) LoadSystemTree(sysCurrentPath);
        }

        private void btnSysRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(sysCurrentPath)) LoadSystemTree(sysCurrentPath);
        }

        private void btnOpenInProject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentProjectPath))
            {
                MessageBox.Show("No hay un proyecto abierto.", "Sin proyecto",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!(tvSystem.SelectedItem is TreeViewItem node) ||
                !(node.Tag is string path) || !File.Exists(path))
            {
                MessageBox.Show("Seleccione un archivo en 'Mis archivos' primero.", "Sin selección",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string dest = Path.Combine(currentProjectPath, Path.GetFileName(path));
            if (File.Exists(dest))
            {
                var r = MessageBox.Show($"'{Path.GetFileName(path)}' ya existe en el proyecto. ¿Sobreescribir?",
                                        "Archivo existente", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }

            try
            {
                File.Copy(path, dest, overwrite: true);
                RefreshTreeView();
                SwitchTab(ActiveTab.Project);
                if (tvFiles.Items.Count > 0 && tvFiles.Items[0] is TreeViewItem root)
                    FindAndSelectNode(root, dest);
                MessageBox.Show($"Archivo copiado al proyecto:\n{dest}", "Listo",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("Error al copiar: " + ex.Message); }
        }

        private void OpenFileInEditor(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                currentFilePath = path;
                txtEditor.Text = File.ReadAllText(path);
                ActualizarNumerosLinea();
                lblProjectName.Content = $"{this.projectName} - {Path.GetFileName(path)}";
                isModified = false;
                txtEditor.Focus();
            }
            catch (Exception ex) { MessageBox.Show("Error al abrir el archivo: " + ex.Message); }
        }

        private void btnSignScript_Click(object sender, RoutedEventArgs e)
        {
            string content = txtEditor.Text;
            string fileName = string.IsNullOrEmpty(currentFilePath)
                ? "sin_nombre.py" : Path.GetFileName(currentFilePath);

            if (string.IsNullOrWhiteSpace(content) || content == "Write your code here...")
            {
                MessageBox.Show("El editor está vacío. Escribe o abre un script primero.",
                                "Nada que firmar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IScript script = new Script(content, fileName);
            var signed = new ScriptSigned(script);

            var result = MessageBox.Show(
                $"Se firmará el script con SHA-256.\n\n" +
                $"Archivo : {fileName}\n" +
                $"Hash    : {signed.Hash}\n\n" +
                $"¿Desea insertar la firma como comentario al inicio del archivo?",
                "Firma SHA-256", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                txtEditor.Text = signed.GetContent();
                ActualizarNumerosLinea();
                isModified = true;
                if (!string.IsNullOrEmpty(currentFilePath)) SaveCurrentFile();
                AppendToConsole($"✔ Script firmado con SHA-256\n   Hash: {signed.Hash}", Brushes.Cyan);
            }
        }

        private void btnHighlightSyntax_Click(object sender, RoutedEventArgs e)
        {
            string content = txtEditor.Text;
            string fileName = string.IsNullOrEmpty(currentFilePath) ? "script.py" : Path.GetFileName(currentFilePath);
            if (string.IsNullOrWhiteSpace(content) || content == "Write your code here...") return;

            IScript script = new Script(content, fileName);
            var formatted = new ScriptFormatted(script);

            txtEditor.Text = formatted.GetContent();
            ActualizarNumerosLinea();
            AppendToConsole("✔ Script formateado.", Brushes.LightGreen);
        }

        private void AppendToConsole(string message, Brush color = null)
        {
            txtConsole.Visibility = Visibility.Visible;
            txtConsoleSeparator.Visibility = Visibility.Visible;
            txtConsole.Foreground = color ?? Brushes.White;
            txtConsole.AppendText(message + Environment.NewLine);
            txtConsole.ScrollToEnd();
        }

        private void stopActiveProcesses()
        {
            if (currentPythonProcess != null && !currentPythonProcess.HasExited)
            {
                currentPythonProcess.Kill();
                currentPythonProcess.Dispose();
                currentPythonProcess = null;
            }
            if (terminalProcess != null && !terminalProcess.HasExited)
            {
                terminalProcess.Kill();
                terminalProcess.Dispose();
                terminalProcess = null;
            }
            Dispatcher.Invoke(() => {
                txtConsole.Clear();
                txtConsole.Foreground = Brushes.White;
            });
        }

        private void openPythonTerminal()
        {
            stopActiveProcesses();
            txtConsole.Visibility = Visibility.Visible;
            txtConsoleSeparator.Visibility = Visibility.Visible;
            spConsoleInput.Visibility = Visibility.Visible;
            txtConsole.Foreground = Brushes.LightGreen;
            txtConsole.AppendText("--- Terminal de Python ---\n");

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = "-u -i",
                WorkingDirectory = !string.IsNullOrEmpty(currentProjectPath)
                                   ? currentProjectPath
                                   : AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };

            terminalProcess = new Process { StartInfo = startInfo };

            terminalProcess.OutputDataReceived += (s, args) =>
            {
                if (args.Data != null)
                    Dispatcher.Invoke(() => {
                        txtConsole.Foreground = Brushes.White;
                        txtConsole.AppendText(args.Data + Environment.NewLine);
                        txtConsole.ScrollToEnd();
                    });
            };
            terminalProcess.ErrorDataReceived += (s, args) =>
            {
                if (args.Data != null)
                    Dispatcher.Invoke(() => {
                        bool welcome = args.Data.StartsWith(">>>") || args.Data.StartsWith("...")
                                    || args.Data.StartsWith("Python ") || args.Data.StartsWith("Type \"");
                        txtConsole.Foreground = welcome ? Brushes.Cyan : Brushes.Red;
                        txtConsole.AppendText(args.Data + Environment.NewLine);
                        txtConsole.ScrollToEnd();
                    });
            };

            terminalProcess.Start();
            terminalProcess.BeginOutputReadLine();
            terminalProcess.BeginErrorReadLine();
            txtConsoleInput.Focus();
        }

        private void btnConsoleSend_Click(object sender, RoutedEventArgs e) => SendConsoleInput();

        private void txtConsoleInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { e.Handled = true; SendConsoleInput(); }
        }

        private void SendConsoleInput()
        {
            try
            {
                string text = txtConsoleInput.Text;
                txtConsole.Foreground = Brushes.Yellow;
                txtConsole.AppendText($">>> {text}{Environment.NewLine}");
                txtConsole.ScrollToEnd();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (currentPythonProcess != null && !currentPythonProcess.HasExited)
                    { currentPythonProcess.StandardInput.WriteLine(text); currentPythonProcess.StandardInput.Flush(); }
                    else if (terminalProcess != null && !terminalProcess.HasExited)
                    { terminalProcess.StandardInput.WriteLine(text); terminalProcess.StandardInput.Flush(); }
                }
                txtConsoleInput.Clear();
                txtConsoleInput.Focus();
            }
            catch (Exception ex) { txtConsole.AppendText($"Error: {ex.Message}{Environment.NewLine}"); }
        }

        private void SetSelectedNodeItalic(bool isItalic)
        {
            if (tvFiles.SelectedItem is TreeViewItem si)
                si.FontStyle = isItalic ? FontStyles.Italic : FontStyles.Normal;
        }

        private void tvFiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (tvFiles.SelectedItem is TreeViewItem item && item.Tag is string path)
            {
                if (item.Header is TextBox txt) txt.SelectAll();
                else StartRename(item, path);
            }
        }

        private void tvFiles_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (item != null) { item.IsSelected = true; item.Focus(); e.Handled = true; }
        }

        private void tvFiles_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (!(tvFiles.SelectedItem is TreeViewItem item) || !(item.Tag is string path))
            { e.Handled = true; return; }

            var cm = new ContextMenu();
            var miRename = new MenuItem { Header = "Renombrar" };
            miRename.Click += (s, ev) => StartRename(item, path);
            var miNew = new MenuItem { Header = "Nuevo archivo" };
            miNew.Click += ctxNewFile_Click;
            var miFolder = new MenuItem { Header = "Nueva carpeta" };
            miFolder.Click += ctxNewFolder_Click;
            var miDel = new MenuItem { Header = "Eliminar" };
            miDel.Click += ctxDelete_Click;

            cm.Items.Add(miRename);
            cm.Items.Add(miNew);
            cm.Items.Add(miFolder);
            cm.Items.Add(new Separator());
            cm.Items.Add(miDel);
            item.ContextMenu = cm;
        }

        private bool FindAndSelectNode(TreeViewItem parent, string path)
        {
            if (parent.Tag is string t && string.Equals(t, path, StringComparison.OrdinalIgnoreCase))
            { parent.IsSelected = true; parent.BringIntoView(); return true; }

            foreach (var item in parent.Items)
            {
                if (item is TreeViewItem node && FindAndSelectNode(node, path))
                { parent.IsExpanded = true; return true; }
            }
            return false;
        }

        private void txtEditor_Click(object sender, RoutedEventArgs e)
        {
            if (txtEditor.Text == "Write your code here..." ||
                txtEditor.Text == "Puedes escribir código de prueba aquí..")
                txtEditor.Text = "";
            e.Handled = true;
        }

        private void RefreshTreeView()
        {
            if (string.IsNullOrEmpty(currentProjectPath)) return;
            tvFiles.Items.Clear();
            var root = new TreeViewItem
            {
                Header = "📁 " + Path.GetFileName(currentProjectPath),
                Tag = currentProjectPath,
                IsExpanded = true,
                Foreground = new SolidColorBrush(Color.FromRgb(135, 206, 235))
            };
            BuildTree(root, currentProjectPath);
            tvFiles.Items.Add(root);
        }

        private void LoadProject(string projectPath)
        {
            try
            {
                this.currentProjectPath = projectPath;
                this.projectName = Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar));
                lblProjectName.Content = this.projectName;
                RefreshTreeView();
                currentFilePath = null;
                txtEditor.Text = "Write your code here...";
                txtLineNumbers.Text = "1";
            }
            catch (Exception ex) { MessageBox.Show("Error loading project: " + ex.Message); }
        }

        private void tvFiles_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (tvFiles.SelectedItem is TreeViewItem node && node.Tag is string path && File.Exists(path))
            {
                try
                {
                    currentFilePath = path;
                    txtEditor.Text = File.ReadAllText(path);
                    ActualizarNumerosLinea();
                    lblProjectName.Content = $"{this.projectName} - {Path.GetFileName(path)}";
                    isModified = false;
                }
                catch (Exception ex) { MessageBox.Show("Error opening file: " + ex.Message); }
            }
        }

        private void BuildTree(TreeViewItem parent, string folder)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(folder))
                {
                    var dirNode = new TreeViewItem
                    {
                        Header = "📁 " + Path.GetFileName(dir),
                        Tag = dir,
                        Foreground = new SolidColorBrush(Color.FromRgb(135, 206, 235))
                    };
                    parent.Items.Add(dirNode);
                    BuildTree(dirNode, dir);
                }
                foreach (var file in Directory.GetFiles(folder, "*.py"))
                {
                    var fileNode = new TreeViewItem
                    {
                        Header = "🐍 " + Path.GetFileName(file),
                        Tag = file,
                        Foreground = new SolidColorBrush(Color.FromRgb(243, 221, 78))
                    };
                    parent.Items.Add(fileNode);
                }
            }
            catch { }
        }

        private Point _startPoint;

        private void tvFiles_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => _startPoint = e.GetPosition(null);

        private void tvFiles_MouseMove(object sender, MouseEventArgs e)
        {
            Point pos = e.GetPosition(null);
            Vector diff = _startPoint - pos;

            if (e.LeftButton == MouseButtonState.Pressed &&
               (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                if (sender is TreeView tree &&
                    tree.SelectedItem is TreeViewItem sel && sel.Tag is string path)
                    DragDrop.DoDragDrop(tree, new DataObject("FilePath", path), DragDropEffects.Move);
            }
        }

        private void tvFiles_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (e.Data.GetDataPresent("FilePath"))
            {
                var item = FindAncestor<TreeViewItem>(tvFiles.InputHitTest(e.GetPosition(tvFiles)) as UIElement);
                if (item != null && item.Tag is string tag && Directory.Exists(tag))
                    e.Effects = DragDropEffects.Move;
            }
            e.Handled = true;
        }

        private void tvFiles_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("FilePath") || string.IsNullOrEmpty(currentProjectPath)) return;
            string src = e.Data.GetData("FilePath") as string;
            if (string.IsNullOrEmpty(src)) return;

            var targetItem = FindAncestor<TreeViewItem>(tvFiles.InputHitTest(e.GetPosition(tvFiles)) as UIElement);
            string targetFolder = currentProjectPath;
            if (targetItem?.Tag is string tag)
            {
                if (Directory.Exists(tag)) targetFolder = tag;
                else if (File.Exists(tag)) targetFolder = Path.GetDirectoryName(tag) ?? currentProjectPath;
            }

            try
            {
                string dest = Path.Combine(targetFolder, Path.GetFileName(src));
                if (!dest.Equals(src, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(src)) File.Move(src, dest);
                    else if (Directory.Exists(src)) Directory.Move(src, dest);
                    LoadProject(currentProjectPath);
                    if (tvFiles.Items.Count > 0 && tvFiles.Items[0] is TreeViewItem root)
                        FindAndSelectNode(root, dest);
                }
            }
            catch (Exception ex) { MessageBox.Show("Error moviendo: " + ex.Message); }
        }

        private void tvFiles_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2 && tvFiles.SelectedItem is TreeViewItem item && item.Tag is string path)
            { StartRename(item, path); e.Handled = true; }
        }

        private void StartRename(TreeViewItem item, string path)
        {
            var tb = new TextBox
            {
                Text = item.Header is TextBox t ? t.Text : item.Header.ToString(),
                Width = 200,
                IsEnabled = true
            };
            tb.KeyDown += (s, e) => {
                if (e.Key == Key.Enter) FinishRename(item, path, tb.Text);
                else if (e.Key == Key.Escape) { item.Header = Path.GetFileName(path); tb.IsEnabled = false; }
            };
            tb.LostFocus += (s, e) => { if (tb.IsEnabled) FinishRename(item, path, tb.Text); };
            item.Header = tb;
            tb.Focus(); tb.SelectAll();
        }

        private void FinishRename(TreeViewItem item, string oldPath, string newName)
        {
            if (string.IsNullOrEmpty(currentProjectPath)) return;
            try
            {
                string newPath = Path.Combine(Path.GetDirectoryName(oldPath) ?? "", newName);
                if (File.Exists(oldPath))
                {
                    if (!Path.GetExtension(newPath).Equals(Path.GetExtension(oldPath), StringComparison.OrdinalIgnoreCase))
                        newPath = Path.ChangeExtension(newPath, Path.GetExtension(oldPath));
                    File.Move(oldPath, newPath);
                }
                else if (Directory.Exists(oldPath))
                    Directory.Move(oldPath, newPath);

                if (oldPath.Equals(currentProjectPath, StringComparison.OrdinalIgnoreCase))
                    currentProjectPath = newPath;

                LoadProject(currentProjectPath);
                if (tvFiles.Items.Count > 0 && tvFiles.Items[0] is TreeViewItem root)
                    FindAndSelectNode(root, newPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error renombrando: " + ex.Message);
                item.Header = Path.GetFileName(oldPath);
            }
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void SaveCurrentFile()
        {
            try
            {
                if (!string.IsNullOrEmpty(currentFilePath))
                {
                    File.WriteAllText(currentFilePath, txtEditor.Text);
                    if (string.IsNullOrEmpty(currentProjectPath))
                        currentProjectPath = Path.GetDirectoryName(currentFilePath);
                    lblProjectName.Content = $"{this.projectName} - {Path.GetFileName(currentFilePath)}";
                    SetSelectedNodeItalic(false);
                    isModified = false;
                }
                else if (!string.IsNullOrEmpty(currentProjectPath))
                {
                    int i = 1; string newPath;
                    do { newPath = Path.Combine(currentProjectPath, $"untitled_{i}.py"); i++; }
                    while (File.Exists(newPath));
                    File.WriteAllText(newPath, txtEditor.Text);
                    RefreshTreeView();
                    if (tvFiles.Items.Count > 0 && tvFiles.Items[0] is TreeViewItem root)
                        FindAndSelectNode(root, newPath);
                    lblProjectName.Content = $"{this.projectName} - {Path.GetFileName(newPath)}";
                    isModified = false;
                }
                else
                    MessageBox.Show("No hay ruta de proyecto disponible.");
            }
            catch (Exception ex) { MessageBox.Show("Error saving file: " + ex.Message); }
        }

        private void btnNewFile_Click(object sender, RoutedEventArgs e) => CreateNewFileOrFolder(true);
        private void btnNewFolder_Click(object sender, RoutedEventArgs e) => CreateNewFileOrFolder(false);

        private void CreateNewFileOrFolder(bool isFile)
        {
            if (string.IsNullOrEmpty(currentProjectPath))
            { MessageBox.Show("No hay proyecto abierto."); return; }

            string target = currentProjectPath;
            if (tvFiles.SelectedItem is TreeViewItem node && node.Tag is string tag)
            {
                if (Directory.Exists(tag)) target = tag;
                else if (File.Exists(tag)) target = Path.GetDirectoryName(tag) ?? currentProjectPath;
            }

            int i = 1; string newPath;
            do { newPath = Path.Combine(target, isFile ? $"NewFile_{i}.py" : $"NewFolder_{i}"); i++; }
            while (isFile ? File.Exists(newPath) : Directory.Exists(newPath));

            if (isFile) File.WriteAllText(newPath, "# new file\n");
            else Directory.CreateDirectory(newPath);

            LoadProject(currentProjectPath);
            if (tvFiles.Items.Count > 0 && tvFiles.Items[0] is TreeViewItem root)
                FindAndSelectNode(root, newPath);
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!(tvFiles.SelectedItem is TreeViewItem node) || node.Tag == null) return;
            string path = node.Tag as string;
            if (string.IsNullOrEmpty(path)) return;

            bool isProject = string.Equals(path, currentProjectPath, StringComparison.OrdinalIgnoreCase);
            string msg = isProject
                ? $"Vas a eliminar todo el proyecto '{Path.GetFileName(path)}'. ¿Continuar?"
                : $"¿Eliminar '{Path.GetFileName(path)}'?";

            if (MessageBox.Show(msg, "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return;

            try
            {
                if (File.Exists(path)) File.Delete(path);
                else if (Directory.Exists(path)) Directory.Delete(path, true);

                if (isProject) this.Close();
                else LoadProject(currentProjectPath ?? "");
            }
            catch (Exception ex) { MessageBox.Show("No se pudo eliminar: " + ex.Message); }
        }

        private void ctxRename_Click(object sender, RoutedEventArgs e)
            => tvFiles_PreviewKeyDown(sender,
               new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), 0, Key.F2)
               { RoutedEvent = Keyboard.KeyDownEvent });

        private void ctxNewFile_Click(object sender, RoutedEventArgs e) => btnNewFile_Click(sender, e);
        private void ctxNewFolder_Click(object sender, RoutedEventArgs e) => btnNewFolder_Click(sender, e);
        private void ctxDelete_Click(object sender, RoutedEventArgs e) => btnDelete_Click(sender, e);

        private void txtEditor_Pasting(object sender, DataObjectPastingEventArgs e) { }
        private void txtEditor_Copying(object sender, DataObjectCopyingEventArgs e) { }

        private async void btnRun_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentFilePath)) SaveCurrentFile();
            stopActiveProcesses();
            lblProjectName.Content = this.projectName + " - Running";
            this.Topmost = false;

            btnRun.IsEnabled = false; btnRun.Visibility = Visibility.Hidden;
            btnStop.IsEnabled = true; btnStop.Visibility = Visibility.Visible;
            txtConsoleSeparator.Visibility = Visibility.Visible;
            txtConsole.Visibility = Visibility.Visible;
            txtConsole.AppendText($"--- Ejecutando: {Path.GetFileName(currentFilePath)} ---\n");

            this.running = true;
            await Task.Run(() =>
            {
                try
                {
                    var start = new ProcessStartInfo
                    {
                        FileName = "python.exe",
                        Arguments = $"-u \"{currentFilePath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(currentFilePath) ?? AppDomain.CurrentDomain.BaseDirectory
                    };

                    currentPythonProcess = Process.Start(start);
                    Dispatcher.Invoke(() => spConsoleInput.Visibility = Visibility.Visible);

                    if (currentPythonProcess != null)
                    {
                        currentPythonProcess.OutputDataReceived += (s, args) =>
                            Dispatcher.Invoke(() => { if (args.Data != null) txtConsole.AppendText(args.Data + Environment.NewLine); });
                        currentPythonProcess.ErrorDataReceived += (s, args) =>
                            Dispatcher.Invoke(() => {
                                if (args.Data != null)
                                { txtConsole.Foreground = Brushes.Red; txtConsole.AppendText(args.Data + Environment.NewLine); }
                            });
                        currentPythonProcess.BeginOutputReadLine();
                        currentPythonProcess.BeginErrorReadLine();
                        currentPythonProcess.WaitForExit();
                    }
                }
                catch (Exception ex)
                { Dispatcher.Invoke(() => txtConsole.AppendText("Error de ejecución: " + ex.Message + Environment.NewLine)); }
                finally
                {
                    currentPythonProcess?.Dispose(); currentPythonProcess = null;
                    this.running = false;
                    Dispatcher.Invoke(() => {
                        spConsoleInput.Visibility = Visibility.Collapsed;
                        this.Topmost = true;
                        lblProjectName.Content = this.projectName;
                        btnRun.Visibility = Visibility.Visible;
                        btnStop.Visibility = Visibility.Collapsed;
                        btnRun.IsEnabled = true;
                        btnStop.IsEnabled = false;
                    });
                }
            });
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            this.running = false;
            try
            {
                if (currentPythonProcess != null && !currentPythonProcess.HasExited)
                {
                    currentPythonProcess.Kill();
                    txtConsole.Foreground = Brushes.Yellow;
                    txtConsole.AppendText(">>> Ejecución detenida por el usuario." + Environment.NewLine);
                }
            }
            catch (Exception ex) { MessageBox.Show("No se pudo detener el proceso: " + ex.Message); }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
            { e.Handled = true; SaveCurrentFile(); }
            else if ((Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.R) || e.Key == Key.F5)
            { e.Handled = true; btnRun_Click(sender, e); }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Q)
            { e.Handled = true; btnReturn_Cick(sender, e); }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.T)
            { e.Handled = true; openPythonTerminal(); }
        }

        private void txtEditor_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0) txtLineNumbers.ScrollToVerticalOffset(e.VerticalOffset);
        }

        private void ActualizarNumerosLinea()
        {
            int count = txtEditor.LineCount;
            var sb = new StringBuilder();
            for (int i = 1; i <= count; i++) sb.AppendLine(i.ToString());
            txtLineNumbers.Text = sb.ToString();
        }

        private void btnReturn_Cick(object sender, RoutedEventArgs e)
        {
            if (isModified)
            {
                var r = MessageBox.Show("¿Desea guardar los cambios antes de salir?", "Cambios sin guardar",
                                        MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (r == MessageBoxResult.Cancel) return;
                if (r == MessageBoxResult.Yes) SaveCurrentFile();
            }
            this.Close();
        }

        private void btnShowFiles_Click(object sender, RoutedEventArgs e)
        {
            if (spFiles.Visibility == Visibility.Visible)
            {
                spFiles.Visibility = Visibility.Collapsed;
                filesSplitter.Visibility = Visibility.Collapsed;
                colPadding.Width = new GridLength(0);
                colFiles.Width = new GridLength(0);
                colSplitter.Width = new GridLength(0);
                colFiles.MinWidth = 0;
            }
            else
            {
                spFiles.Visibility = Visibility.Visible;
                filesSplitter.Visibility = Visibility.Visible;
                colPadding.Width = new GridLength(20);
                colFiles.Width = new GridLength(340);
                colSplitter.Width = new GridLength(3);
                colFiles.MinWidth = 340;
            }
            txtEditor.Focus();
        }

        private void txtEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            ActualizarNumerosLinea();
            isModified = txtEditor.Text != "Write your code here..." &&
                         txtEditor.Text != "Puedes escribir código de prueba aquí..";
            if (txtEditor.IsFocused && !string.IsNullOrEmpty(currentFilePath) && isModified)
            {
                lblProjectName.Content = $"{this.projectName} - {Path.GetFileName(currentFilePath)} - Cambios sin guardar*";
                SetSelectedNodeItalic(true);
            }
        }

        private void BtnCerrarEnunciado_Click(object sender, RoutedEventArgs e)
        {
            pnlEnunciado.Visibility = Visibility.Collapsed;
            homeWorklist.Visibility = Visibility.Visible;
        }

        private void BtnVerEnunciado_Click(object sender, RoutedEventArgs e)
        {
            var tarea = (sender as Button)?.Tag as Assignment;
            if (tarea != null)
            {
                mostrarEnunciado(tarea);
                homeWorklist.Visibility = Visibility.Collapsed;
            }
            else
            {
                homeWorklist.Visibility = Visibility.Collapsed;
                pnlEnunciado.Visibility = Visibility.Visible;
            }
        }

        private void mostrarEnunciado(Assignment tarea)
        {
            if (tarea == null) { pnlEnunciado.Visibility = Visibility.Collapsed; return; }
            lblEnunciadoTitulo.Text = string.IsNullOrWhiteSpace(tarea.Title) ? "(Sin título)" : tarea.Title;
            lblEnunciadoDeadline.Text = tarea.Deadline != DateTime.MinValue
                                        ? "Entrega: " + tarea.Deadline.ToString("g") : string.Empty;
            lblEnunciadoDescripcion.Text = string.IsNullOrWhiteSpace(tarea.Description) ? "(Sin descripción)" : tarea.Description;
            pnlEnunciado.Visibility = Visibility.Visible;
        }

        private void btnTerminal_Click(object sender, RoutedEventArgs e) => openPythonTerminal();
    }

    public class Assignment
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Deadline { get; set; }
    }
}