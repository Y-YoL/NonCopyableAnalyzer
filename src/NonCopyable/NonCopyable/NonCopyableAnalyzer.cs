﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NonCopyable
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NonCopyableAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NoCopy";
        internal const string Title = "non-copyable";
        internal const string MessageFormat = "The type '{0}' is non-copyable";
        internal const string Category = "Correction";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(csc =>
            {
                csc.RegisterOperationAction(oc =>
                {
                    var op = (IArgumentOperation)oc.Operation;
                    var t = op.Value.Type;
                    if (!t.IsNonCopyable()) return;
                    if (op.Parameter.RefKind != RefKind.None) return;
                    if (AllowsCopy(op.Value)) return;

                    oc.ReportDiagnostic(Diagnostic.Create(Rule, op.Syntax.GetLocation(), t.Name));
                }, OperationKind.Argument);

                csc.RegisterOperationAction(oc =>
                {
                    var op = (IConversionOperation)oc.Operation;
                    var t = op.Operand.Type;
                    if (!t.IsNonCopyable()) return;
                    oc.ReportDiagnostic(Diagnostic.Create(Rule, op.Syntax.GetLocation(), t.Name));
                }, OperationKind.Conversion);
            });

            //    OperationKind.ArrayInitializer,
            //    OperationKind.CaseClause,
            //    OperationKind.CollectionElementInitializer,
            //    OperationKind.CompoundAssignment,
            //    OperationKind.DeclarationExpression,
            //    OperationKind.DeclarationPattern,
            //    OperationKind.DeconstructionAssignment,
            //    OperationKind.FieldInitializer,
            //    OperationKind.InterpolatedString,
            //    OperationKind.InterpolatedStringText,
            //    OperationKind.Interpolation,
            //    OperationKind.Invocation,
            //    OperationKind.IsPattern,
            //    OperationKind.IsType,
            //    OperationKind.MemberInitializer,
            //    OperationKind.ObjectOrCollectionInitializer,
            //    OperationKind.ParameterInitializer,
            //    OperationKind.PropertyInitializer,
            //    OperationKind.Return,
            //    OperationKind.SimpleAssignment,
            //    OperationKind.Switch,
            //    OperationKind.Tuple,
            //    OperationKind.UnaryOperator,
            //    OperationKind.BinaryOperator,
            //    OperationKind.VariableDeclaration,
            //    OperationKind.VariableDeclarationGroup,
            //    OperationKind.VariableInitializer,
            //    OperationKind.YieldReturn
        }

        private void AnalyzeOperation(OperationAnalysisContext context)
        {
            ITypeSymbol ltype = null;
            IOperation rvalue = null;

            var semantics = context.Compilation.GetSemanticModel(context.Operation.Syntax.SyntaxTree);
            var conv = semantics.GetConversion(context.Operation.Syntax);
            var a = semantics.GetSpeculativeTypeInfo(0, context.Operation.Syntax, SpeculativeBindingOption.BindAsExpression);
            var b = semantics.GetSpeculativeTypeInfo(0, context.Operation.Syntax, SpeculativeBindingOption.BindAsTypeOrNamespace);

            var xxx = context.Operation.Syntax;
            var list = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                if (xxx == null) break;
                list.Add(xxx.ToString());
                xxx = xxx.Parent;
            }

            switch (context.Operation)
            {
                case IDeconstructionAssignmentOperation o:
                    break;
                case ITupleOperation o:
                    break;
                case IArgumentOperation o:
                    var kind = o.Parameter.RefKind;
                    ltype = o.Parameter.Type;
                    rvalue = o.Value;
                    var s0 = rvalue.Syntax;
                    var s1 = s0.Parent;
                    var s2 = s1.Parent;
                    break;
                case IFieldInitializerOperation o:
                    ltype = o.Type;
                    rvalue = o.Value;
                    break;
                case IReturnOperation o:
                    ltype = o.Type;
                    rvalue = o.ReturnedValue;
            var xx = rvalue?.Type;
                    var p = rvalue.Parent;
                    var ss = o.Syntax;
                    var ssp = o.Syntax.Parent;
                    break;
                default:
                    break;
            }


            if(ltype != null && ltype.IsNonCopyable())
            {

            }
        }

        private static bool AllowsCopy(IOperation op)
        {
            return op.Kind == OperationKind.ObjectCreation;
            //todo: move semantics
        }
    }
}
