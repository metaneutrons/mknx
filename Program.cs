/* SPDX-License-Identifier: GPL-3.0  */

/* Program.cs
 *
 * Author:
 *     Fabian Schmieder <github@schmieder.eu>
 *
 * Copyright(c) 2019 Fabian Schmieder
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using KnxProd;
using KnxProd.Model;

namespace mknx
{
    class Program
    {
        static KnxProdFile k = new KnxProdFile(AppContext.BaseDirectory);

        #region Options

        interface IInitEditOptions
        {
            IEnumerable<string> AddParameterTypes { get; set; }
            IEnumerable<string> RemoveParameterTypes { get; set; }
            IEnumerable<string> AddParameter { get; set; }
            IEnumerable<string> RemoveParameter { get; set; }
            IEnumerable<string> AddComObject { get; set; }
            IEnumerable<string> RemoveComObject { get; set; }
        }

        [Verb("edit", HelpText = "Manipulates KNX-XML definition file.")]
        class EditOptions : IInitEditOptions
        {
            [Option('i', "InputFile", Required = true, HelpText = "name of XML file", MetaValue = "FILE")]
            public string FilenameInput { get; set; }
            [Option('o', "OutputFile", Required = true, HelpText = "name of XML file", MetaValue = "FILE")]
            public string FilenameOutput { get; set; }
            [Option('f', "force", HelpText = "overwrite existing output file")]
            public bool Force { get; set; } = false;

            [Option('n', "SetAppName", Required = false, HelpText = "set KNX application name", MetaValue = "STRING")]
            public string ApplicationName { get; set; }
            [Option('a', "SetAppNumber", Required = false, HelpText = "set KNX application number", MetaValue = "STRING")]
            public ushort? ApplicationNumber { get; set; }
            [Option('a', "SetHardwareName", Required = false, HelpText = "set KNX hardware name", MetaValue = "INT")]
            public string HardwareName { get; set; }
            [Option('s', "SetHardwareSerial", Required = false, HelpText = "set KNX hardware serial", MetaValue = "STRING")]
            public string HardwareSerial { get; set; }
            [Option('m', "SetMediumType", Required = false, HelpText = "set KNX medium type", MetaValue = "STRING")]
            public string MediumType { get; set; }
            [Option('#', "SetOrderNumber", Required = false, HelpText = "set KNX order number", MetaValue = "STRING")]
            public string OrderNumber { get; set; }
            [Option('d', "SetProductName", Required = false, HelpText = "set KNX product name", MetaValue = "STRING")]
            public string ProductName { get; set; }
            [Option('r', "ReplaceVersions", Required = false, HelpText = "set KNX replaced Versions", MetaValue = "STRING")]
            public string ReplacedVersions { get; set; }

            [Option('t', "AddParameterType", Required = false, HelpText = "add KNX parameter type", MetaValue = "\"Name,Type\", \"...")]
            public IEnumerable<string> AddParameterTypes { get; set; }
            [Option('T', "RemoveParameterType", Required = false, HelpText = "remove KNX parameter type", MetaValue = "Name")]
            public IEnumerable<string> RemoveParameterTypes { get; set; }

            [Option('p', "AddParameter", Required = false, HelpText = "add KNX parameter", MetaValue = "Name,Type,Text,Value")]
            public IEnumerable<string> AddParameter { get; set; }
            [Option('P', "RemoveParameter", Required = false, HelpText = "remove KNX parameter", MetaValue = "Name")]
            public IEnumerable<string> RemoveParameter { get; set; }

            [Option('c', "AddComObject", Required = false, HelpText = "add KNX communication object", MetaValue = "Name, Text,FunctionText,Flags = RWCTUI, Priority = alert,hight,low")]
            public IEnumerable<string> AddComObject { get; set; }
            [Option('C', "RemoveComObject", Required = false, HelpText = "remove KNX communication object", MetaValue = "Name")]
            public IEnumerable<string> RemoveComObject { get; set; }
        }

        [Verb("init", HelpText = "Creates new KNX-XML definition file")]
        class InitOptions : EditOptions
        {
            [Option('i', "InputFile", Required = false, Hidden = true, HelpText = "name of XML file", MetaValue = "FILE")]
            new public string FilenameInput { get; set; }
            [Option('n', "SetAppName", Default = "Test Application", Required = true, HelpText = "set KNX application name", MetaValue = "STRING")]
            new public string ApplicationName { get; set; }
            [Option('m', "SetMediumType", Default = "MT-5", Required = false, HelpText = "set KNX medium type", MetaValue = "MT-0,MT1,MT-5")]
            new public string MediumType { get; set; }
        }

        [Verb("make", HelpText = "Creates valid knxprod-file from KNX-XML definition file.")]
        class MakeOptions
        {
            [Option('f', "force", HelpText = "overwrite existing output file")]
            public bool Force { get; set; } = false;
            [Option('i', "InputFile", Required = true, HelpText = "name of XML file", MetaValue = "FILE")]
            public string FilenameInput { get; set; }
            [Option('o', "OutputFile", Required = true, HelpText = "name of knxprod-file", MetaValue = "FILE")]
            public string FilenameOutput { get; set; }
            [Option('e', "Ets4DllPath", Required = false, HelpText = "set path to ETS4 dlls", MetaValue = "PATH")]
            public string Ets4DllPath { get; set; } = "C:\\Program Files (x86)\\ETS4";
        }
        #endregion

        static int Main(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<InitOptions, EditOptions, MakeOptions>(args)
              .MapResult(
                (InitOptions opts) => RunInitAndReturnExitCode(opts),
                (EditOptions opts) => RunEditAndReturnExitCode(opts),
                (MakeOptions opts) => RunMakeAndReturnExitCode(opts),
                errs => 1);
        }

        static private int ModifyModel( IInitEditOptions opts )
        {
            #region Add Data
            // ParamterTypes = Float, Number, Restriction, Text
            foreach (string o in opts.AddParameterTypes)
            {
                List<string> p = o.Split(',').ToList();

                if (p.Count != 2) { Console.WriteLine("Error: AddParameterType '" + p[0] + "' has got wrong amount of arguments."); return 1; }

                ParameterType_T obj = new ParameterType_T
                {
                    Name = p[0]
                };

                switch (p[1].ToLower())
                {
                    case "float":
                        obj.Type = ParameterTypeType.Float;
                        break;
                    case "number":
                        obj.Type = ParameterTypeType.Number;
                        break;
                    case "restriction":
                        obj.Type = ParameterTypeType.Restriction;
                        break;
                    case "text":
                        obj.Type = ParameterTypeType.Text;
                        break;
                    default:
                        Console.WriteLine("Error: AddParameterType '" + p[0] + "' is of unknown type '" + p[1].ToLower() + "'.");
                        return 1; 
                }

                k.ParameterTypes.Add(obj);
                Console.WriteLine("Added Parameter '" + p.First() + "'.");
            }

            // Parameter = Name, Type, Text, Value
            foreach (String o in opts.AddParameter)
            {
                List<string> p = o.Split(',').ToList();

                if (p.Count != 4) { Console.WriteLine("Error: AddParameter '" + p[0] + "' got wrong amount of arguments."); return 1; }

                Parameter_T obj = new Parameter_T
                {
                    Name = p[0]
                };

                if ( k.ParameterTypes.Where(pt => pt.Name == p[1]).Count() != 1 )
                { Console.WriteLine("Error: AddParameter '" + p[0] + "' has got unkown ParameterType '" + p[1] + "'."); return 1; }

                obj.Type = k.ParameterTypes.First(pt => pt.Name == p[1]);

                obj.Text = p[2];
                obj.Value = p[3];

                k.Parameters.Add(obj);
                Console.WriteLine("Added Parameter '" + p.First() + "'.");
            }

            // ComObject = Name, Text, FunctionText, RWCTUI, Prio
            foreach (String o in opts.AddComObject)
            {
                List<string> p = o.Split(',').ToList();

                if (p.Count != 5) { Console.WriteLine("AddComObject '" + p[0] + "' has got wrong amount of arguments."); return 1; }

                ComObject_T obj = new ComObject_T();

                obj.Name = p[0];
                obj.Text = p[1];
                obj.FunctionText = p[2];
                p[3] = p[3].ToUpper();

                obj.ReadFlag = (p[3].Contains('R') ? Enable_T.Enabled : Enable_T.Disabled );
                obj.WriteFlag = (p[3].Contains('W') ? Enable_T.Enabled : Enable_T.Disabled);
                obj.CommunicationFlag = (p[3].Contains('C') ? Enable_T.Enabled : Enable_T.Disabled);
                obj.TransmitFlag = (p[3].Contains('T') ? Enable_T.Enabled : Enable_T.Disabled);
                obj.UpdateFlag = (p[3].Contains('U') ? Enable_T.Enabled : Enable_T.Disabled);
                obj.ReadOnInitFlag = (p[3].Contains('I') ? Enable_T.Enabled : Enable_T.Disabled);

                switch (p[4].ToLower())
                {
                    case "alert":
                        obj.Priority = ComObjectPriority_T.Alert;
                        break;
                    case "high":
                        obj.Priority = ComObjectPriority_T.High;
                        break;
                    case "low":
                        obj.Priority = ComObjectPriority_T.Low;
                        break;
                    default:
                        Console.WriteLine("Error: AddComObject '" + p[0] + "' has got unknown Priority '" + p[1].ToLower() + "'; use 'alert', 'high' or 'low'.");
                        return 1;
                }

                k.ComObjects.Add(obj);
                Console.WriteLine("Added ComObject '" + p.First() + "'.");
            }
            #endregion

            #region Remove Data

            foreach (string o in opts.RemoveParameterTypes)
            {
                ParameterType_T item = k.ParameterTypes.First(pt => pt.Name == o);
                if (item != null)
                {
                    k.ParameterTypes.Remove(item);
                    Console.WriteLine("Removed ParameterType '" + o + "'.");
                }
                else
                {
                    Console.WriteLine("Warning: Could not find ParameterType '" + o + "'.");
                }
            }

            foreach (String o in opts.RemoveParameter)
            {
                Parameter_T item = k.Parameters.First(p => p.Name == o);
                if (item != null)
                {
                    k.Parameters.Remove(item);
                    Console.WriteLine("Removed Parameter '" + o + "'.");
                }
                else
                {
                    Console.WriteLine("Warning: Could not find Parameter '" + o + "'.");
                }
            }

            foreach (String o in opts.RemoveComObject)
            {
                ComObject_T item = k.ComObjects.First(co => co.Name == o);
                if (item != null)
                {
                    k.ComObjects.Remove(item);
                    Console.WriteLine("Removed ComObject '" + o + "'.");
                }
                else
                {
                    Console.WriteLine("Warning: Could not find ComObject '" + o + "'.");
                }
            }
            #endregion

            return 0;
        }

        static private int RunInitAndReturnExitCode( InitOptions opts )
        {
            int retval = 0;

            k.FilenameXML = opts.FilenameOutput;
            k.New();

            k.ApplicationName = opts.ApplicationName;
            k.ApplicationNumber = opts.ApplicationNumber;
            k.HardwareName = opts.HardwareName;
            k.HardwareSerial = opts.HardwareSerial;
            k.MediumType = opts.MediumType;
            k.OrderNumber = opts.OrderNumber;
            k.ProductName = opts.ProductName;
            k.ReplacedVersions = opts.ReplacedVersions;

            // Modify Model

            retval = ModifyModel(opts);
            if (retval > 0) return retval;

            // Write

            if (File.Exists(opts.FilenameOutput) && opts.Force == false)
            {
                Console.WriteLine("Error: file '" + opts.FilenameOutput + "' already exists. Use --force to overwrite.");
                return 1;
            }

            try
            {
                Console.WriteLine("Writing " + opts.FilenameOutput);
                k.Save();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return 1;
            }
        }

        static private int RunEditAndReturnExitCode( EditOptions opts )
        {
            if(opts.ApplicationName != null) k.ApplicationName = opts.ApplicationName;
            if (opts.ApplicationNumber != null) k.ApplicationNumber = opts.ApplicationNumber;
            if (opts.HardwareName != null) k.HardwareName = opts.HardwareName;
            if (opts.HardwareSerial != null) k.HardwareSerial = opts.HardwareSerial;
            if (opts.MediumType != null) k.MediumType = opts.MediumType;
            if (opts.OrderNumber != null) k.OrderNumber = opts.OrderNumber;
            if (opts.ProductName != null) k.ProductName = opts.ProductName;
            if (opts.ReplacedVersions != null) k.ReplacedVersions = opts.ReplacedVersions;

            ModifyModel( opts );

            return 0;
        }

        static private int RunMakeAndReturnExitCode( MakeOptions opts )
        {
            if ( File.Exists(Path.Combine(AppContext.BaseDirectory, "Knx.Ets.Converter.ConverterEngine.dll") ) )
            {
                opts.Ets4DllPath = AppContext.BaseDirectory;
            }
            else if (! File.Exists(Path.Combine(opts.Ets4DllPath, "Knx.Ets.Converter.ConverterEngine.dll") ) )
            {
                Console.WriteLine("Error: Can't find 'Knx.Ets.Converter.ConverterEngine.dll'. Use --Ets4DllPath to set path.");
                return 1;
            }

            if (opts.Ets4DllPath != null) { k.Ets4DllPath = opts.Ets4DllPath; }


            if (!File.Exists(opts.FilenameInput))
            {
                Console.WriteLine("Error: Can't find '"+opts.FilenameInput+"'.");
                return 1;
            }

            k.FilenameXML = opts.FilenameInput;
            k.FilenameKNXprod = opts.FilenameOutput;

            try
            {
                Console.WriteLine("Loading " + opts.FilenameInput);
                k.Load();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return 1;
            }

            try
            {
                Console.WriteLine("Writing " + opts.FilenameOutput);
                k.Export();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.ToString()); // + ex.Message);
                return 1;
            }
        }
    }
}
