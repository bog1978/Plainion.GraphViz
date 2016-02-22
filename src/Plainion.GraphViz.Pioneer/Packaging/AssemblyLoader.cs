﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace Plainion.GraphViz.Pioneer.Packaging
{
    class AssemblyLoader
    {
        private List<Assembly> myAssemblies = new List<Assembly>();
        private Dictionary<string, AssemblyDefinition> myMonoCache = new Dictionary<string, AssemblyDefinition>();
        private List<string> mySkippedAssemblies = new List<string>();

        public IReadOnlyCollection<string> SkippedAssemblies
        {
            get { return mySkippedAssemblies; }
        }

        internal Assembly Load(string path)
        {
            try
            {
                Console.WriteLine("Loading {0}", path);

                var asm = Assembly.LoadFrom(path);
                myAssemblies.Add(asm);
                return asm;
            }
            catch
            {
                Console.WriteLine("ERROR: failed to load assembly {0}", path);
                return null;
            }
        }


        internal AssemblyDefinition MonoLoad(Assembly assembly)
        {
            lock (myMonoCache)
            {
                if (!myMonoCache.ContainsKey(assembly.Location))
                {
                    myMonoCache[assembly.Location] = AssemblyDefinition.ReadAssembly(assembly.Location);
                }
                return myMonoCache[assembly.Location];
            }
        }

        internal Type FindTypeByName(TypeReference typeRef)
        {
            // seems to be always the callers module
            //{
            //    var type = FindTypeByName(typeRef.Module.Assembly.FullName, typeRef);
            //    if (type != null)
            //    {
            //        return type;
            //    }
            //}

            var anr = typeRef.Scope as AssemblyNameReference;
            if (anr != null)
            {
                var type = FindTypeByName(anr.FullName, typeRef);
                if (type != null)
                {
                    return type;
                }
            }

            var md = typeRef.Scope as ModuleDefinition;
            if (md != null)
            {
                var type = FindTypeByName(md.Assembly.FullName, typeRef);
                if (type != null)
                {
                    return type;
                }
            }


            return null;
        }

        private Type FindTypeByName(string assemblyFullName, TypeReference typeRef)
        {
            if (typeRef.FullName == "<Module>")
            {
                return null;
            }

            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .SingleOrDefault(x => x.FullName.Equals(assemblyFullName, StringComparison.OrdinalIgnoreCase));

            if (asm == null)
            {
                // assuming the assembly does not belong to any package
                // -> skip it
                lock (mySkippedAssemblies)
                {
                    if (!mySkippedAssemblies.Contains(assemblyFullName))
                    {
                        mySkippedAssemblies.Add(assemblyFullName);
                    }
                }

                return null;
            }

            var typeFullName = typeRef.FullName;

            if (typeRef.IsGenericInstance)
            {
                var idx = typeFullName.IndexOf('<');
                typeFullName = typeFullName.Substring(0, idx);
            }

            if (typeRef.IsNested)
            {
                typeFullName = typeFullName.Replace('/', '+');
            }

            var type = asm.GetType(typeFullName);

            if (type != null)
            {
                return type;
            }

            //
            // D I A G N O S T I C S
            //

            var ns = typeRef.Namespace;
            if (string.IsNullOrEmpty(ns))
            {
                var idx = typeFullName.LastIndexOf('.');
                ns = typeFullName.Substring(0, idx);
            }

            Console.WriteLine();
            Console.WriteLine(typeFullName);
            Console.WriteLine("  [Asm] " + asm.FullName);
            Console.WriteLine("  [NS]  " + ns);


            foreach (var t in asm.GetTypes().Where(x => x.Namespace == ns))
            {
                Console.WriteLine("  + " + t.FullName);
            }

            Environment.Exit(2);

            return null;
        }
    }
}
