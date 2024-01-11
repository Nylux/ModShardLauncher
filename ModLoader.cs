﻿using System.Collections.Generic;
using ModShardLauncher.Mods;
using System.Linq;
using System.Windows;
using UndertaleModLib;
using System.Diagnostics;
using UndertaleModLib.Decompiler;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows.Controls;
using UndertaleModLib.Models;
using ModShardLauncher.Extensions;
using UndertaleModLib.Compiler;
using System.Text;
using System.Xml.Linq;
using ModShardLauncher.Pages;
using System.Windows.Navigation;
using System.Net;

namespace ModShardLauncher
{
    public class ModLoader
    {
        internal static UndertaleData Data => DataLoader.data;
        public static string ModPath => Path.Join(Environment.CurrentDirectory, "Mods");
        public static string ModSourcesPath => Path.Join(Environment.CurrentDirectory, "ModSources");
        public static List<string> ModScripts = new List<string>();
        public static Dictionary<string, ModFile> Mods = new Dictionary<string, ModFile>();
        public static Dictionary<string, ModSource> ModSources = new Dictionary<string, ModSource>();
        private static List<Assembly> Assemblies = new List<Assembly>();
        private static bool patched = false;
        public static List<string> Weapons;
        public static List<string> WeaponDescriptions;
        public static Dictionary<string, Action<string>> ScriptCallbacks = new Dictionary<string, Action<string>>();
        public static void ShowMessage(string msg)
        {
            Trace.Write(msg);
        }
        public static void Initalize()
        {
            Weapons = GetTable("gml_GlobalScript_table_weapons");
            WeaponDescriptions = GetTable("gml_GlobalScript_table_weapons_text");
            
        }
        public static void InitCallbacks()
        {
            ScriptCallbacks.Add("getInstances", (str) =>
            {
                var DynamicObject = JsonConvert.DeserializeObject<dynamic>(str);
                NavigationWindow nw = new NavigationWindow();
                var page = new InstanceInfos();
                var nodes = page.Infos.Items;
                nodes.Clear();

                var root = new TreeViewItem() { Header = "Instances" };
                nodes.Add(root);

                foreach (var item in DynamicObject.Children())
                {
                    var data = item.ToString().Split(' ');
                    List<byte> bytes = new List<byte>();
                    foreach (var i in data[0].Split('|'))
                        bytes.AddRange(BitConverter.GetBytes(int.Parse(i)));
                    bytes.RemoveAll(t => t == 0);
                    var instance = new TreeViewItem()
                    {
                        Header = (data[0].Split('|')[0].Length <= 3
                        ? Encoding.UTF8.GetString(bytes.ToArray())
                        : Encoding.Unicode.GetString(bytes.ToArray()))
                        + " : " + data[1] + " " + data[2]
                    };
                    root.Items.Add(instance);
                }

                root.IsExpanded = true;
                nw.Width = 300;
                nw.Height = 450;
                nw.Navigate(page);
                nw.Show();
            });
            ScriptCallbacks.Add("getInstanceById", (str) =>
            {
                var DynamicObject = JsonConvert.DeserializeObject<string[]>(str);
                NavigationWindow nw = new NavigationWindow();
                var page = new InstanceInfos();
                var nodes = page.Infos.Items;
                nodes.Clear();

                var root = new TreeViewItem() { Header = "Instance" };
                nodes.Add(root);

                foreach (var item in DynamicObject)
                {
                    var data = item.Split(' ');
                    var instance = new TreeViewItem();
                    var bytes = new List<byte>();
                    if (data[2].Contains("|"))
                    {
                        foreach (var i in data[2].Split('|'))
                            bytes.AddRange(BitConverter.GetBytes(int.Parse(i)));
                        instance = new TreeViewItem() { Header = data[0] + " : " + Encoding.Unicode.GetString(bytes.ToArray()) };
                    }
                    else instance = new TreeViewItem() { Header = item };
                    root.Items.Add(instance);
                }

                root.IsExpanded = true;
                nw.Width = 300;
                nw.Height = 450;
                nw.Navigate(page);
                nw.Show();
            });
            ScriptCallbacks.Add("getWeaponDataById", (str) =>
            {
                var DynamicObject = JsonConvert.DeserializeObject<dynamic>(str);
                NavigationWindow nw = new NavigationWindow();
                var page = new InstanceInfos();
                var nodes = page.Infos.Items;
                nodes.Clear();

                var root = new TreeViewItem() { Header = "Weapon" };
                nodes.Add(root);
                foreach (var item in DynamicObject.Children())
                {
                    var data = item.ToString();
                    if (data.Contains("|"))
                    {
                        var bytes = new List<byte>();
                        var value = data.Split(' ')[1].Replace("\"", "").Split('|');
                        foreach (var i in value)
                            bytes.AddRange(BitConverter.GetBytes(int.Parse(i)));
                        data = data.Split(' ')[0] + " " + Encoding.Unicode.GetString(bytes.ToArray());
                    }
                    var node = new TreeViewItem() { Header = data };
                    root.Items.Add(node);
                }

                root.IsExpanded = true;
                nw.Width = 300;
                nw.Height = 450;
                nw.Navigate(page);
                nw.Show();
            });
        }
        public static UndertaleGameObject AddObject(string name)
        {
            var obj = new UndertaleGameObject()
            {
                Name = Data.Strings.MakeString(name)
            };
            if(Data.GameObjects.FirstOrDefault(t => t.Name.Content == name) == default)
                Data.GameObjects.Add(obj);
            return obj;
        }
        public static UndertaleGameObject GetObject(string name)
        {
            return Data.GameObjects.FirstOrDefault(t => t.Name.Content == name);
        }
        public static UndertaleSprite GetSprite(string name)
        {
            return Data.Sprites.FirstOrDefault(t => t.Name.Content == name);
        }
        public static void SetObject(string name, UndertaleGameObject o)
        {
            var obj = Data.GameObjects.First(t => t.Name.Content.IndexOf(name) != -1);
            Data.GameObjects[Data.GameObjects.IndexOf(obj)] = o;
        }
        public static UndertaleCode AddCode(string Code, string name)
        {
            var code = new UndertaleCode();
            var locals = new UndertaleCodeLocals();
            code.Name = Data.Strings.MakeString(name);
            locals.Name = code.Name;
            UndertaleCodeLocals.LocalVar argsLocal = new UndertaleCodeLocals.LocalVar();
            argsLocal.Name = Data.Strings.MakeString("arguments");
            argsLocal.Index = 0;
            locals.Locals.Add(argsLocal);
            code.LocalsCount = 1;
            Data.CodeLocals.Add(locals);
            code.ReplaceGML(Code, Data);
            Data.Code.Add(code);
            return code;
        }
        internal static UndertaleCode AddInnerCode(string name) => AddCode(GetCodeRes(name), name);
        public static UndertaleCode AddFunction(string Code, string name)
        {
            var scriptCode = AddCode(Code, name);
            ModScripts.Add(name);
            Data.Code.Add(Data.Code[0]);
            Data.Code.RemoveAt(0);
            return scriptCode;
        }
        internal static UndertaleCode AddInnerFunction(string name) => AddFunction(GetCodeRes(name), name);
        public static List<string> GetTable(string name)
        {
            var table = Data.Code.First(t => t.Name.Content.IndexOf(name) != -1);
            GlobalDecompileContext context = new GlobalDecompileContext(Data, false);
            var text = Decompiler.Decompile(table, context);
            var ret = Regex.Match(text, "return (\\[.*\\])").Groups[1].Value;
            return JsonConvert.DeserializeObject<List<string>>(ret);
        }
        public static UndertaleCode GetCode(string name)
        {
            var code = Data.Code.First(t => t.Name.Content == name);
            return code;
        }
        public static string GetDecompiledCode(string name)
        {
            var func = Data.Code.First(t => t.Name.Content == name);
            GlobalDecompileContext context = new GlobalDecompileContext(Data, false);
            var text = Decompiler.Decompile(func, context);
            return text;
        }
        public static string GetDisassemblyCode(string name)
        {
            var func = Data.Code.First(t => t.Name.Content == name);
            var text = func.Disassemble(Data.Variables, Data.CodeLocals.For(func));
            
            return text;
        }
        public static void SetDecompiledCode(string Code, string name)
        {
            var code = Data.Code.First(t => t.Name.Content == name);
            code.ReplaceGML(Code, Data);
        }
        public static void InsertDecompiledCode(string Code, string name, int pos)
        {
            var code = GetDecompiledCode(name).Split("\n").ToList();
            code.Insert(pos, Code);
            SetDecompiledCode(string.Join("\n", code), name);
        }
        public static void ReplaceDecompiledCode(string Code, string name, int pos)
        {
            var code = GetDecompiledCode(name).Split("\n").ToList();
            code[pos] = Code;
            SetDecompiledCode(string.Join("\n", code), name);
        }
        public static void SetDisassemblyCode(string Code, string name)
        {
            var code = Data.Code.First(t => t.Name.Content == name);
            code.ReplaceGML(Code, Data);
        }
        public static void SetTable(List<string> table, string name)
        {
            var ret = JsonConvert.SerializeObject(table).Replace("\n", "");
            var target = Data.Code.First(t => t.Name.Content.IndexOf(name) != -1);
            GlobalDecompileContext context = new GlobalDecompileContext(Data, false);
            var text = Decompiler.Decompile(target, context);
            text = Regex.Replace(text, "\\[.*\\]", ret);
            target.ReplaceGML(text, Data);
        }
        public static Weapon GetWeapon(string ID)
        {
            var str = Weapons.First(t => t.StartsWith(ID));
            var descs = WeaponDescriptions.FindAll(t => t.StartsWith(ID))[1].Split(";").ToList();
            descs.Remove("");
            descs.RemoveAt(0);
            var names = WeaponDescriptions.First(t => t.StartsWith(ID)).Split(";").ToList();
            names.Remove("");
            names.RemoveAt(0);
            var weapon = new Weapon(str, descs, names);
            return weapon;
        }
        public static void SetWeapon(string ID, Weapon weapon)
        {
            var target = Weapons.First(t => t.StartsWith(ID));
            var name = WeaponDescriptions.First(t => t.StartsWith(ID));
            var desc = WeaponDescriptions.FindAll(t => t.StartsWith(ID))[1];
            var index = Weapons.IndexOf(target);
            var index2 = WeaponDescriptions.IndexOf(desc);
            var index3 = WeaponDescriptions.IndexOf(name);
            Weapons[index] = Weapon.Weapon2String(weapon).Item1;
            WeaponDescriptions[index2] = Weapon.Weapon2String(weapon).Item2;
            WeaponDescriptions[index3] = Weapon.Weapon2String(weapon).Item3;
        }
        public static void LoadFiles()
        {
            var mods = Main.Instance.ModPage.Mods;
            var modSources = Main.Instance.ModSourcePage.ModSources;
            foreach(ModFile i in mods)
                if(i.Stream != null) i.Stream.Close();
            var modCaches = new List<ModFile>();
            Mods.Clear();
            modSources.Clear();
            ModSources.Clear();
            var sources = Directory.GetDirectories(ModSourcesPath);
            foreach(var i in sources)
            {
                var info = new ModSource()
                {
                    Name = i.Split("\\").Last(),
                    Path = i
                };
                modSources.Add(info);
                ModSources.Add(info.Name, info);
            }
            var files = Directory.GetFiles(ModPath, "*.sml");
            foreach (var file in files)
            {
                var f = FileReader.Read(file);
                if (f == null) continue;
                Assembly assembly = f.Assembly;
                if (assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Mod))).Count() == 0)
                {
                    MessageBox.Show("加载错误: " + assembly.GetName().Name + " 此Mod需要一个Mod类");
                    continue;
                }
                else
                {
                    var type = assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Mod))).ToList()[0];
                    var mod = Activator.CreateInstance(type) as Mod;
                    mod.LoadAssembly();
                    mod.ModFiles = f;
                    f.instance = mod;
                    var old = mods.FirstOrDefault(t => t.Name == f.Name);
                    if (old != null) f.isEnabled = old.isEnabled;
                    modCaches.Add(f);
                }
                Assemblies.Add(assembly);
            }
            mods.Clear();
            modCaches.ForEach(i => {
                mods.Add(i);
                Mods.Add(i.Name, i);
            });
        }
        public static void PatchMods()
        {
            Assembly ass = Assembly.GetEntryAssembly();
            var mods = ModInfos.Instance.Mods;
            foreach (ModFile mod in mods)
            {
                if (!mod.isEnabled) continue;
                if (!mod.isExisted)
                {
                    MessageBox.Show(Application.Current.FindResource("ModLostWarning").ToString() + " : " + mod.Name);
                    continue;
                }
                Main.Settings.EnableMods.Add(mod.Name);
                var version = DataLoader.GetVersion();
                var reg = new Regex("0([0-9])");
                version = reg.Replace(version, "$1");
                if (mod.Version != version)
                {
                    var result = MessageBox.Show(Application.Current.FindResource("VersionDifferentWarning").ToString(),
                        Application.Current.FindResource("VersionDifferentWarningTitle").ToString() + " : " + mod.Name, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.No) continue;
                }
                TextureLoader.LoadTextures(mod);
                mod.instance.PatchMod();
                var modAss = mod.Assembly;
                Type[] types = modAss.GetTypes().Where(t => !t.IsAbstract).ToArray();
                foreach (var type in types)
                {
                    if (type.IsSubclassOf(typeof(Weapon))) 
                        LoadWeapon(type);
                    if (type.IsSubclassOf(typeof(ModHooks)))
                        LoadHooks(type);
                }
            }
        }
        public static void LoadHooks(Type type)
        {
            var hooks = Activator.CreateInstance(type);
            var instance = ModInterfaceEngine.Instance as ModInterfaceEngine;
            foreach (var hook in type.GetMethods())
            {
                if (instance.HookDelegates.ContainsKey(hook.Name))
                    instance.HookDelegates[hook.Name] += (object[] obj) =>
                    {
                        hook.Invoke(hooks, new object[] { obj });
                    };
            }
            instance.IsLoadHooks = true;
        }
        public static void LoadWeapon(Type type)
        {
            var weapon = Activator.CreateInstance(type) as Weapon;
            weapon.SetDefaults();
            var strs = weapon.AsString();
            Weapons.Insert(Weapons.IndexOf("SWORDS - BLADES;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;") + 1, strs.Item1);
            WeaponDescriptions.Insert(WeaponDescriptions.IndexOf(";;SWORDS;;;;;;SWORDS;SWORDS;;;;") + 1, weapon.Name + ";" + string.Join(";", weapon.NameList.Values));
            WeaponDescriptions.Insert(WeaponDescriptions.IndexOf(";weapon_desc;weapon_desc;weapon_desc;weapon_desc;weapon_desc;weapon_desc;weapon_desc;weapon_desc;weapon_desc;weapon_desc;weapon_desc;weapon_desc;") + 1,
                weapon.Name + ";" + string.Join(";", weapon.WeaponDescriptions.Values));
            WeaponDescriptions.Insert(WeaponDescriptions.IndexOf(";weapon_pronoun;weapon_pronoun;weapon_pronoun;weapon_pronoun;weapon_pronoun;weapon_pronoun;weapon_pronoun;weapon_pronoun;weapon_pronoun;weapon_pronoun;weapon_pronoun;weapon_pronoun;") + 1,
                weapon.Name + ";He;;;It;She;She;She;She;He;;;;");
        }
        public static async void PatchFile()
        {
            PatchInnerFile();
            PatchMods();
            AddFunction("function ModScripts() \n{ \n\treturn " + new UndertaleString(string.Join(",", ModScripts)) + "\n}", "ModScripts");
            AddFunction("function ModPath() \n{ \n\treturn " + new UndertaleString(ModPath) + "\n}", "ModPath");
            AddFunction("function EnableMods() \n{ \n\treturn " + new UndertaleString(string.Join(",", Main.Settings.EnableMods)) + "\n}", "EnableMods");
            foreach (var item in ModScripts)
            {
                ModInterfaceEngine.Instance.SetPropertyValue(item, new Action<object[]>((object[] objects) =>
                {
                    var script = item + " " + string.Join(" ", objects);
                    ModInterfaceServer.SendScript(script);
                }));
            }
            SetTable(Weapons, "gml_GlobalScript_table_weapons");
            SetTable(WeaponDescriptions, "gml_GlobalScript_table_weapons_text");
            LoadFiles();
        }
        internal static void PatchInnerFile()
        {
            AddInnerFunction("print");
            AddInnerFunction("give");
            AddInnerFunction("getInstances");
            AddInnerFunction("getInstanceById");
            AddInnerFunction("getWeaponDataById");
            AddInnerFunction("editWeaponDataById");
            AddInnerFunction("SendMsg");
            AddExtension(new ModShard());
            var engine = AddObject("o_ScriptEngine");
            engine.Persistent = true;
            var ev = new UndertaleGameObject.Event();
            ev.EventSubtypeOther = EventSubtypeOther.AsyncNetworking;
            ev.Actions.Add(new UndertaleGameObject.EventAction()
            {
                CodeId = AddInnerCode("ScriptEngine_server")
            });
            engine.Events[7].Add(ev);
            var create = new UndertaleGameObject.Event();
            create.Actions.Add(new UndertaleGameObject.EventAction()
            {
                CodeId = AddInnerCode("ScriptEngine_create")
            });
            engine.Events[0].Add(create);
            var start = Data.Rooms.First(t => t.Name.Content == "START");
            var newObj = new UndertaleRoom.GameObject()
            {
                ObjectDefinition = engine,
                InstanceID = Data.GeneralInfo.LastObj++
            };

            start.GameObjects.Add(newObj);
        }
        public static void AddExtension(UndertaleExtensionFile file)
        {
            var ext = Data.Extensions.First(t => t.Name.Content == "display_mouse_lock");
            ext.Files.Add(file);
        }
        internal static string GetCodeRes(string name)
        {
            var data = CodeResources.ResourceManager.GetObject(name, CodeResources.Culture) as byte[];
            if (data?[0] == 239 && data[1] == 187 && data[2] == 191) data = data.Skip(3).ToArray();
            var text = Encoding.UTF8.GetString(data);
            return text;
        }
    }
}
