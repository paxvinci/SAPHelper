using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security;
using System.Security.Cryptography;
using System.IO;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ManageCompositeRole
{
    class LambdaComparer<T> : IEqualityComparer<T>
    {
        private readonly Func<T, T, bool> _lambdaComparer;
        private readonly Func<T, int> _lambdaHash;

        public LambdaComparer(Func<T, T, bool> lambdaComparer) :
            this(lambdaComparer, o => 0)
        {
        }

        public LambdaComparer(Func<T, T, bool> lambdaComparer, Func<T, int> lambdaHash)
        {
            if (lambdaComparer == null)
                throw new ArgumentNullException("lambdaComparer");
            if (lambdaHash == null)
                throw new ArgumentNullException("lambdaHash");

            _lambdaComparer = lambdaComparer;
            _lambdaHash = lambdaHash;
        }

        public bool Equals(T x, T y)
        {
            return _lambdaComparer(x, y);
        }

        public int GetHashCode(T obj)
        {
            return _lambdaHash(obj);
        }
    }

    static class EncryptProtectData
    {
        static byte[] entropy = System.Text.Encoding.Unicode.GetBytes("Salt Is Not A Password");

        public static string EncryptString(SecureString input)
        {
            byte[] encryptedData = ProtectedData.Protect(
                Encoding.Unicode.GetBytes(ToInsecureString(input)), entropy,
                DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedData);
        }

        public static SecureString DecryptString(string encryptedData)
        {
            try
            {
                byte[] decryptedData = ProtectedData.Unprotect(
                    Convert.FromBase64String(encryptedData), entropy,
                    DataProtectionScope.CurrentUser);
                return ToSecureString(Encoding.Unicode.GetString(decryptedData));
            }
            catch
            {
                return new SecureString();
            }
        }

        public static SecureString ToSecureString(string input)
        {
            SecureString secure = new SecureString();
            foreach (char c in input)
            {
                secure.AppendChar(c);
            }
            secure.MakeReadOnly();
            return secure;
        }

        public static string ToInsecureString(SecureString input)
        {
            string returnValue = string.Empty;
            IntPtr ptr = Marshal.SecureStringToBSTR(input);
            try
            {
                returnValue = Marshal.PtrToStringBSTR(ptr);
            }
            finally
            {
                Marshal.ZeroFreeBSTR(ptr);
            }
            return returnValue;
        }

    }

    public class ProxyParameter
    {
        public String ProxyID { get; set; }
        public String AppServerHost { get; set; }
        public String SAPRouter { get; set; }
        public String Client { get; set; }
        public String User { get; set; }
        public String Password { get; set; }
        public String SystemNumber { get; set; }
        public String Language { get; set; }
        public String PoolSize { get; set; }
        public String MaxPoolSize { get; set; }
        public String IdleTimeout { get; set; }
    }

    public static class EnumHelper
    {
        /// <summary>
        /// Gets an attribute on an enum field value
        /// </summary>
        /// <typeparam name="T">The type of the attribute you want to retrieve</typeparam>
        /// <param name="enumVal">The enum value</param>
        /// <returns>The attribute of type T that exists on the enum value</returns>
        public static T GetAttributeOfType<T>(this Enum enumVal) where T : System.Attribute
        {
            var type = enumVal.GetType();
            var memInfo = type.GetMember(enumVal.ToString());
            var attributes = memInfo[0].GetCustomAttributes(typeof(T), false);
            return (attributes.Length > 0) ? (T)attributes[0] : null;
        }
    }

    public static class EnumExtensions
    {

        // This extension method is broken out so you can use a similar pattern with 
        // other MetaData elements in the future. This is your base method for each.
        public static T GetAttribute<T>(this Enum value) where T : Attribute
        {
            var type = value.GetType();
            var memberInfo = type.GetMember(value.ToString());
            var attributes = memberInfo[0].GetCustomAttributes(typeof(T), false);
            return (T)attributes[0];
        }

        // This method creates a specific call to the above method, requesting the
        // Description MetaData attribute.
        public static string ToName(this Enum value)
        {
            var attribute = value.GetAttribute<DisplayAttribute>();
            return attribute == null ? value.ToString() : attribute.Name;
        }
    }

    public enum Operations
    {
        [Display(Name = "Attribuzione ruolo singolo a collettivo")]
        AddRoleToComposite,
        [Display(Name = "Cancella attribuzione ruolo singolo a collettivo")]
        DelRoleToComposite,
        [Display(Name = "Nuovo ruolo collettivo")]
        NewComposite,
        [Display(Name = "Nuovo ruolo singolo")]
        NewSingle,
        [Display(Name = "Copia ruolo singolo")]
        CopyRole,
        [Display(Name = "Rinomina ruolo")]
        RenameRole,
        [Display(Name = "Elimina attribuzione transazione a ruolo")]
        DelTCodes,
        [Display(Name = "Attribuzione transazione a ruolo")]
        AddTCodes,
        [Display(Name = "Cancella ruolo")]
        DeleteRole,
        [Display(Name = "Creazione completa ruoli con transazioni")]
        CompleteNewSingle,
        [Display(Name = "Assegna ruolo ad utente")]
        AssignRole,
        [Display(Name = "Disassegna ruolo ad utente")]
        RemoveRole,
        [Display(Name ="Crea ruolo derivato")]
        DerivateRole,
        [Display(Name = "Esporta uno spool su file di testo")]
        DownloadSpoolJob,
        [Display(Name = "Esporta i campi di tabelle")]
        ExportFieldTable,
    }

    class CompositeRoleHelper
    {
        public static String SerializeParams(ProxyParameter param)
        {
            StringWriter writer = new StringWriter();
            Type thisType = typeof(ProxyParameter);
            System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(thisType);
            x.Serialize(writer, param);
            return writer.ToString();
        }

        public static ProxyParameter DeSerializeParams(String xmlText)
        {
            Type thisType = typeof(ProxyParameter);
            System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(thisType);
            using (TextReader reader = new StringReader(xmlText))
            {
                try
                {
                    ProxyParameter param = (ProxyParameter)x.Deserialize(reader);
                    return param;
                }
                catch (Exception exc)
                {
                }
                return null;
            }
            //this.ProxyID = proxy.ProxyID;
            //this.AppServerHost = proxy.AppServerHost;
            //this.Client = proxy.Client;
            //this.User = proxy.User;
            //this.Password = proxy.Password;
            //this.Language = proxy.Language;
            //this.PoolSize = proxy.PoolSize;
            //this.MaxPoolSize = proxy.MaxPoolSize;
            //this.IdleTimeout = proxy.IdleTimeout;
        }
    }
}
