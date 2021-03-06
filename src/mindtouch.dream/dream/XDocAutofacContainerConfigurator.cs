﻿/*
 * MindTouch Dream - a distributed REST framework 
 * Copyright (C) 2006-2014 MindTouch, Inc.
 * www.mindtouch.com  oss@mindtouch.com
 *
 * For community documentation and downloads visit mindtouch.com;
 * please review the licensing section.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autofac;
using Autofac.Builder;
using Autofac.Core;
using log4net;
using MindTouch.Xml;

namespace MindTouch.Dream {

    /// <summary>
    /// Autofac <see cref="Module"/> implemenation for Dream specific configuration xml.
    /// </summary>
    public class XDocAutofacContainerConfigurator : Module, IEnumerable<Type> {

        //--- Types ---
        private class DebugStringBuilder {

            //--- Fields ---
            private readonly StringBuilder _builder;

            //--- Constructors ---
            public DebugStringBuilder(bool enabled) {
                if(enabled) {
                    _builder = new StringBuilder();
                }
            }

            //--- Methods ---
            public void AppendFormat(string format, params object[] args) {
                if(_builder == null) {
                    return;
                }
                _builder.AppendFormat(format, args);
            }

            public void Append(string str) {
                if(_builder == null) {
                    return;
                }
                _builder.Append(str);
            }

            public override string ToString() {
                return _builder == null ? null : _builder.ToString();
            }
        }

        //--- Class Fields ---
        private static readonly ILog _log = LogUtils.CreateLog();

        //--- Fields ---
        private readonly XDoc _config;
        private readonly DreamContainerScope _defaultScope;

        //--- Constructors ---

        /// <summary>
        /// Create a new XDoc configurator.
        /// </summary>
        /// <param name="config">Configuration fragment.</param>
        /// <param name="defaultScope">Default registration scope.</param>
        public XDocAutofacContainerConfigurator(XDoc config, DreamContainerScope defaultScope) {
            if(config == null) {
                throw new ArgumentNullException("config");
            }
            _defaultScope = defaultScope;
            _config = config;
        }

        //--- Methods ---

        /// <summary>
        /// Load the configuration into a builder
        /// </summary>
        /// <param name="builder">Container builder to public.</param>
        protected override void Load(ContainerBuilder builder) {
            if(builder == null) {
                throw new ArgumentNullException("builder");
            }

            foreach(var component in _config["component"]) {
                var componentDebug = new DebugStringBuilder(_log.IsDebugEnabled);
                var implementationTypename = component["@implementation"].AsText;
                var type = LoadType(component["@type"]);
                IRegistrationBuilder<object, ConcreteReflectionActivatorData, SingleRegistrationStyle> registrar;
                var name = component["@name"].AsText;
                if(string.IsNullOrEmpty(implementationTypename)) {
                    componentDebug.AppendFormat("registering concrete type '{0}'", type.FullName);
                    registrar = builder.RegisterType(type);
                } else {
                    var concreteType = LoadType(implementationTypename);
                    componentDebug.AppendFormat("registering concrete type '{0}' as '{1}'", concreteType.FullName, type.FullName);
                    registrar = builder.RegisterType(concreteType).As(new TypedService(type));
                }
                if(!string.IsNullOrEmpty(name)) {
                    registrar.Named(name, type);
                    componentDebug.AppendFormat("named '{0}'", name);
                }
                registrar.WithParameters(
                    (from parameter in component["parameters/parameter"]
                     let parameterName = parameter["@name"].AsText
                     let parameterValue = parameter["@value"].AsText
                     select new ResolvedParameter(
                         (p, c) => p.Name == parameterName,
                         (p, c) => SysUtil.ChangeType(parameterValue, p.ParameterType))
                     ).Cast<Parameter>());

                // set scope
                DreamContainerScope scope = _defaultScope;
                var strScope = component["@scope"].AsText;
                if(strScope != null) {
                    scope = SysUtil.ParseEnum<DreamContainerScope>(strScope);
                }
                componentDebug.AppendFormat(" in '{0}' scope", scope);
                registrar.InScope(scope);
                if(_log.IsDebugEnabled) {
                    _log.Debug(componentDebug.ToString());
                }
            }
        }

        private Type LoadType(XDoc type) {
            return LoadType(type.AsText);
        }

        private Type LoadType(string typeName) {
            if(string.IsNullOrEmpty(typeName)) {
                throw new ArgumentNullException("typeName");
            }
            Type type = Type.GetType(typeName);
            if(type == null) {
                throw new ArgumentException(string.Format("Type {0} could not be loaded", typeName));
            }
            return type;
        }

        public IEnumerator<Type> GetEnumerator() {
            return (from component in _config["component"]
                    select LoadType(component["@type"])
                    ).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
