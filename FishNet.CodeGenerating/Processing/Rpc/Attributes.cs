﻿using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Connection;
using FishNet.Object.Helping;
using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace FishNet.CodeGenerating.Processing.Rpc
{
    internal class Attributes : CodegenBase
    {

        /// <summary>
        /// Returns if methodDef has any Rpc attribute.
        /// </summary>
        public bool HasRpcAttributes(MethodDefinition methodDef)
        {
            foreach (CustomAttribute customAttribute in methodDef.CustomAttributes)
            {
                RpcType rt = base.Session.GetClass<AttributeHelper>().GetRpcAttributeType(customAttribute);
                if (rt != RpcType.None)
                    return true;
            }

            //Fall through, nothing found.
            return false;
        }

        /// <summary>
        /// Returns a collection of RpcAttribute for methodDef.
        /// </summary>
        public List<AttributeData> GetRpcAttributes(MethodDefinition methodDef)
        {
            List<AttributeData> results = new List<AttributeData>();
            string asyncAttributeFullName = typeof(AsyncStateMachineAttribute).FullName;
            bool isAsync = false;

            foreach (CustomAttribute customAttribute in methodDef.CustomAttributes)
            {
                RpcType rt = base.Session.GetClass<AttributeHelper>().GetRpcAttributeType(customAttribute);
                if (rt != RpcType.None)
                {
                    results.Add(new AttributeData(customAttribute, rt));
                }
                //Not a rpc attribute.
                else
                {
                    //Check if async.
                    if (customAttribute.Is(asyncAttributeFullName))
                        isAsync = true;
                }
            }

            //Nothing found, exit early.
            if (results.Count == 0)
            {
                return results;
            }
            //If has at least one RPC attrivbute and is an async method.
            else if (isAsync)
            {
                base.Session.LogError(
                    $"{methodDef.Name} is an async RPC. This feature is not currently supported. You may instead run an async method from this RPC.",
                    methodDef.DebugInformation.SequencePoints.FirstOrDefault()
                );
                return new List<AttributeData>();
            }
            //If more than one attribute make sure the combination is allowed.
            else if (results.Count >= 2)
            {
                RpcType allRpcTypes = results.GetCombinedRpcType();
                if (allRpcTypes != (RpcType.Observers | RpcType.Target))
                {
                    base.Session.LogError(
                        $"{methodDef.Name} contains multiple RPC attributes. Only ObserversRpc and TargetRpc attributes may be combined.",
                        methodDef.DebugInformation.SequencePoints.FirstOrDefault()
                    );
                    return new List<AttributeData>();
                }
            }

            //Next validate that the method is setup properly for each rpcType.
            foreach (AttributeData ad in results)
            {
                //If not valid then return empty list.
                if (!IsRpcMethodValid(methodDef, ad.RpcType))
                    return new List<AttributeData>();
            }

            return results;
        }

        /// <summary>
        /// Returns if a RpcMethod can be serialized and has a proper signature.
        /// </summary>
        private bool IsRpcMethodValid(MethodDefinition methodDef, RpcType rpcType)
        {
            //Static method.
            if (methodDef.IsStatic)
            {
                base.Session.LogError(
                    $"{methodDef.Name} RPC method cannot be static.", methodDef.DebugInformation.SequencePoints.FirstOrDefault());
                return false;
            }
            //Is generic type.
            else if (methodDef.HasGenericParameters)
            {
                base.Session.LogError(
                    $"{methodDef.Name} RPC method cannot contain generic parameters.",
                    methodDef.DebugInformation.SequencePoints.FirstOrDefault()
                );
                return false;
            }
            //Abstract method.
            else if (methodDef.IsAbstract)
            {
                base.Session.LogError(
                    $"{methodDef.Name} RPC method cannot be abstract.",
                    methodDef.DebugInformation.SequencePoints.FirstOrDefault()
                );
                return false;
            }
            //Non void return.
            else if (methodDef.ReturnType != methodDef.Module.TypeSystem.Void)
            {
                base.Session.LogError(
                    $"{methodDef.Name} RPC method must return void.",
                    methodDef.DebugInformation.SequencePoints.FirstOrDefault()
                );
                return false;
            }
            //Misc failing conditions.
            else
            {
                //Check for async attribute.
                foreach (CustomAttribute ca in methodDef.CustomAttributes)
                {

                }
            }
            //TargetRpc but missing correct parameters.
            if (rpcType == RpcType.Target)
            {
                if (methodDef.Parameters.Count == 0 || !methodDef.Parameters[0].Is(typeof(NetworkConnection)))
                {
                    base.Session.LogError(
                        $"Target RPC {methodDef.Name} must have a NetworkConnection as the first parameter.",
                        methodDef.DebugInformation.SequencePoints.FirstOrDefault()
                    );
                    return false;
                }
            }

            //Make sure all parameters can be serialized.
            for (int i = 0; i < methodDef.Parameters.Count; i++)
            {
                ParameterDefinition parameterDef = methodDef.Parameters[i];

                //If NetworkConnection, TargetRpc, and first parameter.
                if ((i == 0) && (rpcType == RpcType.Target) && parameterDef.Is(typeof(NetworkConnection)))
                    continue;

                if (parameterDef.ParameterType.IsGenericParameter)
                {
                    base.Session.LogError(
                        $"RPC method{methodDef.Name} contains a generic parameter. This is currently not supported.",
                        methodDef.DebugInformation.SequencePoints.FirstOrDefault()
                    );
                    return false;
                }

                //Can be serialized/deserialized.
                bool canSerialize = base.GetClass<GeneralHelper>().HasSerializerAndDeserializer(parameterDef.ParameterType, true);
                if (!canSerialize)
                {
                    base.Session.LogError(
                        $"RPC method {methodDef.Name} parameter type {parameterDef.ParameterType.FullName} does not support serialization. Use a supported type or create a custom serializer.",
                        methodDef.DebugInformation.SequencePoints.FirstOrDefault()
                    );
                    return false;
                }

            }

            //Fall through, success.
            return true;
        }

    }
}