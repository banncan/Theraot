﻿#if LESSTHAN_NET35

#pragma warning disable CC0021 // Use nameof

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Dynamic.Utils;
using System.Runtime.CompilerServices;
using System.Text;

namespace System.Linq.Expressions.Interpreter
{
    public class LightLambda
    {
        private readonly IStrongBox[] _closure;

        private readonly Interpreter _interpreter;
#if NO_FEATURE_STATIC_DELEGATE
        private static readonly CacheDict<Type, Func<LightLambda, Delegate>> _runCache = new CacheDict<Type, Func<LightLambda, Delegate>>(100);
#endif

        internal LightLambda(LightDelegateCreator delegateCreator, IStrongBox[] closure)
        {
            _closure = closure;
            _interpreter = delegateCreator.Interpreter;
        }

        internal string DebugView => new DebugViewPrinter(_interpreter).ToString();

        public object Run(params object[] arguments)
        {
            var frame = MakeFrame();
            for (var i = 0; i < arguments.Length; i++)
            {
                frame.Data[i] = arguments[i];
            }

            var currentFrame = frame.Enter();
            try
            {
                _interpreter.Run(frame);
            }
            finally
            {
                for (var i = 0; i < arguments.Length; i++)
                {
                    arguments[i] = frame.Data[i];
                }

                frame.Leave(currentFrame);
            }

            return frame.Pop();
        }

        public object RunVoid(params object[] arguments)
        {
            var frame = MakeFrame();
            for (var i = 0; i < arguments.Length; i++)
            {
                frame.Data[i] = arguments[i];
            }

            var currentFrame = frame.Enter();
            try
            {
                _interpreter.Run(frame);
            }
            finally
            {
                for (var i = 0; i < arguments.Length; i++)
                {
                    arguments[i] = frame.Data[i];
                }

                frame.Leave(currentFrame);
            }

            return null;
        }

        internal Delegate MakeDelegate(Type delegateType)
        {
#if !NO_FEATURE_STATIC_DELEGATE
            var method = delegateType.GetInvokeMethod();
            return method.ReturnType == typeof(void) ? DelegateHelpers.CreateObjectArrayDelegate(delegateType, RunVoid) : DelegateHelpers.CreateObjectArrayDelegate(delegateType, Run);

#else
            Func<LightLambda, Delegate> fastCtor = GetRunDelegateCtor(delegateType);
            if (fastCtor != null)
            {
                return fastCtor(this);
            }
            else
            {
                return CreateCustomDelegate(delegateType);
            }
#endif
        }

        private InterpretedFrame MakeFrame()
        {
            return new InterpretedFrame(_interpreter, _closure);
        }

        private class DebugViewPrinter
        {
            private readonly Dictionary<int, string> _handlerEnter = new Dictionary<int, string>();
            private readonly Dictionary<int, int> _handlerExit = new Dictionary<int, int>();
            private readonly Interpreter _interpreter;
            private readonly Dictionary<int, int> _tryStart = new Dictionary<int, int>();
            private string _indent = "  ";

            public DebugViewPrinter(Interpreter interpreter)
            {
                _interpreter = interpreter;

                Analyze();
            }

            public override string ToString()
            {
                var sb = new StringBuilder();

                var name = _interpreter.Name ?? "lambda_method";
                sb.Append("object ").Append(name).AppendLine("(object[])");
                sb.AppendLine("{");

                sb.Append("  .locals ").Append(_interpreter.LocalCount).AppendLine();
                sb.Append("  .maxstack ").Append(_interpreter.Instructions.MaxStackDepth).AppendLine();
                sb.Append("  .maxcontinuation ").Append(_interpreter.Instructions.MaxContinuationDepth).AppendLine();
                sb.AppendLine();

                var instructions = _interpreter.Instructions.Instructions;
                var debugView = new InstructionArray.DebugView(_interpreter.Instructions);
                var instructionViews = debugView.GetInstructionViews();

                for (var i = 0; i < instructions.Length; i++)
                {
                    EmitExits(sb, i);

                    if (_tryStart.TryGetValue(i, out var startCount))
                    {
                        for (var j = 0; j < startCount; j++)
                        {
                            sb.Append(_indent).AppendLine(".try");
                            sb.Append(_indent).AppendLine("{");
                            Indent();
                        }
                    }

                    if (_handlerEnter.TryGetValue(i, out var handler))
                    {
                        sb.Append(_indent).AppendLine(handler);
                        sb.Append(_indent).AppendLine("{");
                        Indent();
                    }

                    var instructionView = instructionViews[i];

                    sb.Append(_indent).Append("IP_").AppendFormat("{0:0000}", i).Append(": ").AppendLine(instructionView.GetValue());
                }

                EmitExits(sb, instructions.Length);

                sb.AppendLine("}");

                return sb.ToString();
            }

            private void AddHandlerExit(int index)
            {
                _handlerExit[index] = _handlerExit.TryGetValue(index, out var count) ? count + 1 : 1;
            }

            private void AddTryStart(int index)
            {
                if (!_tryStart.TryGetValue(index, out var count))
                {
                    _tryStart.Add(index, 1);
                    return;
                }

                _tryStart[index] = count + 1;
            }

            private void Analyze()
            {
                var instructions = _interpreter.Instructions.Instructions;

                foreach (var instruction in instructions)
                {
                    switch (instruction)
                    {
                        case EnterTryCatchFinallyInstruction enterTryCatchFinally:
                            var handler = enterTryCatchFinally.Handler;

                            AddTryStart(handler.TryStartIndex);
                            AddHandlerExit(handler.TryEndIndex + 1 /* include Goto instruction that acts as a "leave" */);

                            if (handler.IsFinallyBlockExist)
                            {
                                _handlerEnter.Add(handler.FinallyStartIndex, "finally");
                                AddHandlerExit(handler.FinallyEndIndex);
                            }

                            if (handler.IsCatchBlockExist)
                            {
                                foreach (var catchHandler in handler.Handlers)
                                {
                                    _handlerEnter.Add(catchHandler.HandlerStartIndex - 1 /* include EnterExceptionHandler instruction */, catchHandler.ToString());
                                    AddHandlerExit(catchHandler.HandlerEndIndex);

                                    var filter = catchHandler.Filter;
                                    if (filter == null)
                                    {
                                        continue;
                                    }

                                    _handlerEnter.Add(filter.StartIndex - 1 /* include EnterExceptionFilter instruction */, "filter");
                                    AddHandlerExit(filter.EndIndex);
                                }
                            }

                            break;

                        default:
                            break;
                    }

                    if (!(instruction is EnterTryFaultInstruction enterTryFault))
                    {
                        continue;
                    }

                    {
                        var handler = enterTryFault.Handler;

                        AddTryStart(handler.TryStartIndex);
                        AddHandlerExit(handler.TryEndIndex + 1 /* include Goto instruction that acts as a "leave" */);

                        _handlerEnter.Add(handler.FinallyStartIndex, "fault");
                        AddHandlerExit(handler.FinallyEndIndex);
                    }
                }
            }

            private void Dedent()
            {
                _indent = new string(' ', _indent.Length - 2);
            }

            private void EmitExits(StringBuilder sb, int index)
            {
                if (!_handlerExit.TryGetValue(index, out var exitCount))
                {
                    return;
                }

                for (var j = 0; j < exitCount; j++)
                {
                    Dedent();
                    sb.Append(_indent).AppendLine("}");
                }
            }

            private void Indent()
            {
                _indent = new string(' ', _indent.Length + 2);
            }
        }

#if NO_FEATURE_STATIC_DELEGATE
        private static Func<LightLambda, Delegate> GetRunDelegateCtor(Type delegateType)
        {
            lock (_runCache)
            {
                Func<LightLambda, Delegate> fastCtor;
                if (_runCache.TryGetValue(delegateType, out fastCtor))
                {
                    return fastCtor;
                }
                return MakeRunDelegateCtor(delegateType);
            }
        }

        private static Func<LightLambda, Delegate> MakeRunDelegateCtor(Type delegateType)
        {
            MethodInfo method = delegateType.GetInvokeMethod();
            ParameterInfo[] paramInfos = method.GetParameters();
            Type[] paramTypes;
            string name = "Run";

            if (paramInfos.Length >= MaxParameters)
            {
                return null;
            }

            if (method.ReturnType == typeof(void))
            {
                name += "Void";
                paramTypes = new Type[paramInfos.Length];
            }
            else
            {
                paramTypes = new Type[paramInfos.Length + 1];
                paramTypes[paramTypes.Length - 1] = method.ReturnType;
            }

            MethodInfo runMethod;

            if (method.ReturnType == typeof(void) && paramTypes.Length == 2 &&
                paramInfos[0].ParameterType.IsByRef && paramInfos[1].ParameterType.IsByRef)
            {
                runMethod = typeof(LightLambda).GetMethod("RunVoidRef2", BindingFlags.NonPublic | BindingFlags.Instance);
                paramTypes[0] = paramInfos[0].ParameterType.GetElementType();
                paramTypes[1] = paramInfos[1].ParameterType.GetElementType();
            }
            else if (method.ReturnType == typeof(void) && paramTypes.Length == 0)
            {
                runMethod = typeof(LightLambda).GetMethod("RunVoid0", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            else
            {
                for (int i = 0; i < paramInfos.Length; i++)
                {
                    paramTypes[i] = paramInfos[i].ParameterType;
                    if (paramTypes[i].IsByRef)
                    {
                        return null;
                    }
                }

#if FEATURE_MAKE_RUN_METHODS
                if (DelegateHelpers.MakeDelegate(paramTypes) == delegateType)
                {
                    name = "Make" + name + paramInfos.Length;

                    MethodInfo ctorMethod = typeof(LightLambda).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(paramTypes);
                    return _runCache[delegateType] = (Func<LightLambda, Delegate>)ctorMethod.CreateDelegate(typeof(Func<LightLambda, Delegate>));
                }
#endif

                runMethod = typeof(LightLambda).GetMethod(name + paramInfos.Length, BindingFlags.NonPublic | BindingFlags.Instance);
            }

            /*
            try {
                DynamicMethod dm = new DynamicMethod("FastCtor", typeof(Delegate), new[] { typeof(LightLambda) }, typeof(LightLambda), true);
                var ilGenerator = dm.GetILGenerator();
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldftn, runMethod.IsGenericMethodDefinition ? runMethod.MakeGenericMethod(paramTypes) : runMethod);
                ilGenerator.Emit(OpCodes.Newobj, delegateType.GetConstructor(new[] { typeof(object), typeof(IntPtr) }));
                ilGenerator.Emit(OpCodes.Ret);
                return _runCache[delegateType] = (Func<LightLambda, Delegate>)dm.CreateDelegate(typeof(Func<LightLambda, Delegate>));
            } catch (SecurityException) {
            }*/

            // we don't have permission for restricted skip visibility dynamic methods, use the slower Delegate.CreateDelegate.
            var targetMethod = runMethod.IsGenericMethodDefinition ? runMethod.MakeGenericMethod(paramTypes) : runMethod;
            return _runCache[delegateType] = lambda => targetMethod.CreateDelegate(delegateType, lambda);
        }

        //TODO enable sharing of these custom delegates
        private Delegate CreateCustomDelegate(Type delegateType)
        {
            //PerfTrack.NoteEvent(PerfTrack.Categories.Compiler, "Synchronously compiling a custom delegate");

            MethodInfo method = delegateType.GetInvokeMethod();
            ParameterInfo[] paramInfos = method.GetParameters();
            var parameters = new ParameterExpression[paramInfos.Length];
            var parametersAsObject = new Expression[paramInfos.Length];
            bool hasByRef = false;
            for (int i = 0; i < paramInfos.Length; i++)
            {
                ParameterExpression parameter = Expression.Parameter(paramInfos[i].ParameterType, paramInfos[i].Name);
                hasByRef = hasByRef || paramInfos[i].ParameterType.IsByRef;
                parameters[i] = parameter;
                parametersAsObject[i] = Expression.Convert(parameter, typeof(object));
            }

            NewArrayExpression data = Expression.NewArrayInit(typeof(object), parametersAsObject);
            var dlg = new Func<object[], object>(Run);

            ConstantExpression dlgExpr = Expression.Constant(dlg);

            ParameterExpression argsParam = Expression.Parameter(typeof(object[]), "$args");

            Expression body;
            if (method.ReturnType == typeof(void))
            {
                body = Expression.Block(typeof(void), Expression.Invoke(dlgExpr, argsParam));
            }
            else
            {
                body = Expression.Convert(Expression.Invoke(dlgExpr, argsParam), method.ReturnType);
            }

            if (hasByRef)
            {
                List<Expression> updates = new List<Expression>();
                for (int i = 0; i < paramInfos.Length; i++)
                {
                    if (paramInfos[i].ParameterType.IsByRef)
                    {
                        updates.Add(
                            Expression.Assign(
                                parameters[i],
                                Expression.Convert(
                                    Expression.ArrayAccess(argsParam, Expression.Constant(i)),
                                    paramInfos[i].ParameterType.GetElementType()
                                )
                            )
                        );
                    }
                }

                body = Expression.TryFinally(body, Expression.Block(typeof(void), updates));
            }

            body = Expression.Block(
                method.ReturnType,
                new[] { argsParam },
                Expression.Assign(argsParam, data),
                body
            );

            var lambda = Expression.Lambda(delegateType, body, parameters);
            //return System.Linq.Expressions.Compiler.LambdaCompiler.Compile(lambda, null);
            throw new NotImplementedException("byref delegate");
        }
#endif
#if NO_FEATURE_STATIC_DELEGATE
        internal void RunVoidRef2<T0, T1>(ref T0 arg0, ref T1 arg1)
        {
            // copy in and copy out for today...
            var frame = MakeFrame();
            frame.Data[0] = arg0;
            frame.Data[1] = arg1;
            var currentFrame = frame.Enter();
            try
            {
                _interpreter.Run(frame);
            }
            finally
            {
                frame.Leave(currentFrame);
                arg0 = (T0)frame.Data[0];
                arg1 = (T1)frame.Data[1];
            }
        }
#endif
    }
}

#endif