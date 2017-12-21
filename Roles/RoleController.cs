using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using System.Reflection;
using System.Threading;
using System.ComponentModel;
using System.Data;

namespace ManageCompositeRole.Roles
{
    class RoleController
    {
        RoleModel role;

        public RoleController(ProxyParameter param)
        {
            role = new RoleModel(param);
        }

        public void Refresh()
        {
            role.Refresh();
        }

        public List<KeyValuePair<String, String>> GetRoles()
        {
            var result = (from a in role.RolesData.Tables["AGR_DEFINE"].AsEnumerable()
                          join t in role.RolesData.Tables["AGR_TEXTS"].AsEnumerable()
                          on a["AGR_NAME"] equals t["AGR_NAME"]
                          where t["LINE"].ToString() == "00000"
                          select new KeyValuePair<String, String>(a["AGR_NAME"].ToString(), t["TEXT"].ToString()))
                          .ToList<KeyValuePair<String, String>>();
            return result;
        }

        public object GetRoleProperties(String selectedRole)
        {
            // Define the dynamic assembly, module and type
            AssemblyName assemblyName = new AssemblyName("DynamicAssembly");
            AssemblyBuilder assemblyBuilder =
                Thread.GetDomain().DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");
            TypeBuilder typeBuilder = moduleBuilder.DefineType("DynamicType", TypeAttributes.Public);

            // Create dynamic properties corresponding to query results
            foreach (DataColumn row in role.RolesData.Tables["AGR_DEFINE"].Columns)
            {
                string name = (string)row.ColumnName;
                string category = "Role Properties";
                string description = "";
                Type dataType = row.DataType;

                this.BuildProperty(typeBuilder, name, category, description, dataType);
            }

            // Create and instantiate the dynamic type
            Type type = typeBuilder.CreateType();
            Object dynamicType = Activator.CreateInstance(type, new object[] { });

            var dt = (from r in role.RolesData.Tables["AGR_DEFINE"].AsEnumerable()
                      where (r["AGR_NAME"].ToString() == selectedRole)
                      select r);
            // Set each property's default value
            foreach (DataRow row in dt)
            {
                foreach (DataColumn col in role.RolesData.Tables["AGR_DEFINE"].Columns)
                {
                    string name = (string)col.ColumnName;
                    Type dataType = col.DataType;
                    object value = row[col];

                    value = (Convert.IsDBNull(value)) ? null : Convert.ChangeType(value, dataType);
                    type.InvokeMember(name,
                                        BindingFlags.SetProperty,
                                        null,
                                        dynamicType,
                                        new object[] { value });
                }
            }
            return dynamicType;
        }


        protected void BuildProperty(TypeBuilder typeBuilder,
                                        string name,
                                        string category,
                                        string description,
                                        Type fieldType)
        {
            // Generate the private field/public property name pair 
            // (field begins w/LC, property begins w/UC)
            char[] chars = name.ToCharArray();

            chars[0] = char.ToLower(chars[0]);
            string fieldName = new string(chars);

            chars[0] = char.ToUpper(chars[0]);
            string propertyName = new string(chars);

            // Create the private field
            FieldBuilder fieldBuilder = typeBuilder.DefineField(name,
                                                                    fieldType,
                                                                    FieldAttributes.Private);

            // Create the corresponding public property
            PropertyBuilder propertyBuilder =
                typeBuilder.DefineProperty(propertyName,
                                            System.Reflection.PropertyAttributes.HasDefault,
                                            fieldType,
                                            null);

            // Define the required set of property attributes
            MethodAttributes propertyAttributes = MethodAttributes.Public |
                                                    MethodAttributes.SpecialName |
                                                    MethodAttributes.HideBySig;

            // Build the getter
            MethodBuilder getter = typeBuilder.DefineMethod("get_" + propertyName,
                                                                propertyAttributes,
                                                                fieldType,
                                                                Type.EmptyTypes);
            ILGenerator getterIlGen = getter.GetILGenerator();
            getterIlGen.Emit(OpCodes.Ldarg_0);
            getterIlGen.Emit(OpCodes.Ldfld, fieldBuilder);
            getterIlGen.Emit(OpCodes.Ret);

            // Build the setter
            MethodBuilder setter = typeBuilder.DefineMethod("set_" + propertyName,
                                                                propertyAttributes,
                                                                null,
                                                                new Type[] { fieldType });
            ILGenerator setterIlGen = setter.GetILGenerator();
            setterIlGen.Emit(OpCodes.Ldarg_0);
            setterIlGen.Emit(OpCodes.Ldarg_1);
            setterIlGen.Emit(OpCodes.Stfld, fieldBuilder);
            setterIlGen.Emit(OpCodes.Ret);

            // Bind the getter and setter
            propertyBuilder.SetGetMethod(getter);
            propertyBuilder.SetSetMethod(setter);

            // Set the Category and Description attributes
            propertyBuilder.SetCustomAttribute(
                new CustomAttributeBuilder(
                    typeof(CategoryAttribute).GetConstructor(
                        new Type[] { typeof(string) }), new object[] { category }));
            propertyBuilder.SetCustomAttribute(
                new CustomAttributeBuilder(
                    typeof(DescriptionAttribute).GetConstructor(
                        new Type[] { typeof(string) }), new object[] { description }));
        }
    }
}
