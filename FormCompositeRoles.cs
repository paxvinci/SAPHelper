using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Security;
using ManageCompositeRole.Roles;
using System.Reflection;

namespace ManageCompositeRole
{
    public partial class FormCompositeRoles : Form
    {
        private ProxyParameter proxy;
        private RoleController roleController;

        public FormCompositeRoles()
        {
            InitializeComponent();
            proxy = new ProxyParameter();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
            ManageCompositeRole conn = new ManageCompositeRole();
            conn.OperationStatus += new LogEventHandler(conn_operationStatus);
            conn.CompleteStatus += new CompleteEventHandler(conn_CompleteStatus);
            conn.ErrorStatus += conn_ErrorStatus;

            conn.Operation = (Operations)Enum.Parse(typeof(Operations), (cmbActions.SelectedItem as DataRowView).Row[0].ToString());
            //if (radioButton1.Checked)
            //    conn.Operation = Operations.NewSingle;
            //if (radioButton2.Checked)
            //    conn.Operation = Operations.RenameRole;
            //if (radioButton3.Checked)
            //    conn.Operation = Operations.DelRoleToComposite;
            //if (radioButton4.Checked)
            //    conn.Operation = Operations.AddRoleToComposite;
            //if (radioButton5.Checked)
            //    conn.Operation = Operations.NewComposite;
            //if (radioButton6.Checked)
            //    conn.Operation = Operations.DelTCodes;
            //if (radioButton7.Checked)
            //    conn.Operation = Operations.AddTCodes;
            //if (radioButton8.Checked)
            //    conn.Operation = Operations.DeleteRole;

            if (conn.Operation == null)
            {
                MessageBox.Show("Seleziona un'operazione");
                return;
            }

            proxy.AppServerHost = textBox6.Text;
            proxy.SAPRouter = textBox8.Text;
            proxy.SystemNumber = textBox3.Text;
            proxy.Client = textBox5.Text;
            proxy.User = textBox4.Text;
            proxy.Password = textBox1.Text;
            proxy.Language = textBox2.Text;
            this.Cursor = Cursors.WaitCursor;
            conn.Login(proxy);
            conn.DoOnCompositeRoles(textBox7.Text);
            conn.Disconnect();
            this.Cursor = this.DefaultCursor;
        }

        void conn_ErrorStatus(object sender, LogEventArgs e)
        {
            MessageBox.Show("Error: " + e.Log);
        }

        void conn_CompleteStatus(object sender, LogEventArgs e)
        {
            MessageBox.Show("Completato!");
        }

        void conn_operationStatus(object sender, LogEventArgs e)
        {
            listBox1.Items.Add(e.Log);
            listBox1.Refresh();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button2.Visible = false;
            DialogResult res = openFileDialog1.ShowDialog(this);
            if (res == System.Windows.Forms.DialogResult.OK || res == System.Windows.Forms.DialogResult.Yes)
            {
                textBox7.Text = openFileDialog1.FileName;
                button2.Visible = true;
            }
        }

        private void FormCompositeRoles_Load(object sender, EventArgs e)
        {
            if (!File.Exists("cache"))
                return;
            String encryptedString = File.ReadAllText("cache", Encoding.UTF8);
            SecureString sString = EncryptProtectData.DecryptString(encryptedString);
            String decryptedSerialized = EncryptProtectData.ToInsecureString(sString);

            proxy = CompositeRoleHelper.DeSerializeParams(decryptedSerialized);
            if (proxy == null)
                proxy = new ProxyParameter();

            proxy.ProxyID = Guid.NewGuid().ToString();
            proxy.MaxPoolSize = "10";
            proxy.PoolSize = "1";
            proxy.IdleTimeout = "10";
            textBox6.Text = proxy.AppServerHost;
            textBox3.Text = proxy.SystemNumber;
            textBox5.Text = proxy.Client;
            textBox4.Text = proxy.User;
            textBox1.Text = proxy.Password;
            textBox2.Text = proxy.Language;
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("Operation"), new DataColumn("Description") });
            Array items = Enum.GetValues(typeof(Operations));
            foreach (Operations item in items)
                dt.LoadDataRow(new object[] { Enum.GetName(typeof(Operations), item), item.ToName() }, true);
            cmbActions.DataSource = dt;
            cmbActions.DisplayMember = "Description";
        }

        private void FormCompositeRoles_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!e.Cancel && proxy != null)
            {
                SecureString sString = EncryptProtectData.ToSecureString(CompositeRoleHelper.SerializeParams(proxy));
                String encryptedSerialized = EncryptProtectData.EncryptString(sString);
                File.WriteAllText("cache", encryptedSerialized, Encoding.Unicode);
            }
        }

        private void treeView1_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            if (!String.IsNullOrWhiteSpace(e.Node.Text))
                pgRoles.SelectedObject = roleController.GetRoleProperties(e.Node.Text);
        }

        private void tabPage3_Enter(object sender, EventArgs e)
        {
            roleController = new RoleController(proxy);
        }

        private void tsbRefresh_Click(object sender, EventArgs e)
        {
            roleController.Refresh();
            roleController.GetRoles().ForEach(r => treeView1.Nodes.Add(new TreeNode(r.Key, new TreeNode[] { new TreeNode(r.Value) })));
        }

        private void textBox6_TextChanged(object sender, EventArgs e)
        {
            if (sender.GetType() == typeof(TextBox))
            {
                TextBox txt = (sender as TextBox);
                PropertyInfo propertyInfo = proxy.GetType().GetProperty(txt.Tag.ToString());
                propertyInfo.SetValue(proxy, Convert.ChangeType(txt.Text, propertyInfo.PropertyType), null);
            }
        }

        private void tabPage4_Enter(object sender, EventArgs e)
        {

        }
    }
}
