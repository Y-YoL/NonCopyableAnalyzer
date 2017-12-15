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
                    var op = (ISymbolInitializerOperation)oc.Operation;
                    CheckCopyability(oc, op.Value);
                }, OperationKind.FieldInitializer,
                OperationKind.ParameterInitializer,
                OperationKind.PropertyInitializer,
                OperationKind.VariableInitializer);

                csc.RegisterOperationAction(oc =>
                {
                    // including member initializer
                    // including collection element initializer
                    var op = (ISimpleAssignmentOperation)oc.Operation;
                    if (op.IsRef) return;
                    CheckCopyability(oc, op.Value);
                }, OperationKind.SimpleAssignment);

                csc.RegisterOperationAction(oc =>
                {
                    // including non-ref extension method invocation
                    var op = (IArgumentOperation)oc.Operation;
                    if (op.Parameter.RefKind != RefKind.None) return;
                    CheckCopyability(oc, op.Value);
                }, OperationKind.Argument);

                csc.RegisterOperationAction(oc =>
                {
                    var op = (IConversionOperation)oc.Operation;
                    CheckCopyDisallowed(oc, op.Operand);
                }, OperationKind.Conversion);

                csc.RegisterOperationAction(oc =>
                {
                    var op = (IArrayInitializerOperation)oc.Operation;

                    if (!((IArrayTypeSymbol)((IArrayCreationOperation)op.Parent).Type).ElementType.IsNonCopyable()) return;

                    foreach (var v in op.ElementValues)
                    {
                        CheckCopyability(oc, v);
                    }
                }, OperationKind.ArrayInitializer);

                csc.RegisterOperationAction(oc =>
                {
                    var op = (ICollectionElementInitializerOperation)oc.Operation;

                    if (!HasNonCopyableParameter(op.AddMethod)) return;

                    foreach (var a in op.Arguments)
                    {
                        CheckCopyability(oc, a);
                    }
                }, OperationKind.CollectionElementInitializer);

                csc.RegisterOperationAction(oc =>
                {
                    var op = (IDeclarationPatternOperation)oc.Operation;
                    var t = ((ILocalSymbol)op.DeclaredSymbol).Type;
                    if (!t.IsNonCopyable()) return;
                    oc.ReportDiagnostic(Diagnostic.Create(Rule, op.Syntax.GetLocation(), t.Name));
                }, OperationKind.DeclarationPattern);

                csc.RegisterOperationAction(oc =>
                {
                    var op = (ITupleOperation)oc.Operation;

                    // exclude ParenthesizedVariableDesignationSyntax
                    if (op.Syntax.Kind() != SyntaxKind.TupleExpression) return;

                    foreach (var v in op.Elements)
                    {
                        CheckCopyability(oc, v);
                    }
                }, OperationKind.Tuple);
            });

            //    OperationKind.CompoundAssignment,
            //    OperationKind.Return,
            //    OperationKind.Tuple,
            //    OperationKind.YieldReturn

            // not supported yet:
            //    OperationKind.UnaryOperator,
            //    OperationKind.BinaryOperator,
        }

        private static bool HasNonCopyableParameter(IMethodSymbol m)
        {
            foreach (var p in m.Parameters)
            {
                if(p.RefKind == RefKind.None)
                {
                    if (p.Type.IsNonCopyable()) return true;
                }
            }
            return false;
        }

        private static void CheckCopyability(OperationAnalysisContext oc, IOperation v)
        {
            var t = v.Type;
            if (!t.IsNonCopyable()) return;
            if (AllowsCopy(v)) return;
            oc.ReportDiagnostic(Diagnostic.Create(Rule, v.Syntax.GetLocation(), t.Name));
        }

        private static void CheckCopyDisallowed(OperationAnalysisContext oc, IOperation v)
        {
            var t = v.Type;
            if (!t.IsNonCopyable()) return;
            oc.ReportDiagnostic(Diagnostic.Create(Rule, v.Syntax.GetLocation(), t.Name));
        }

        private static bool AllowsCopy(IOperation op)
        {
            var k = op.Kind;

            if(k == OperationKind.Conversion)
            {
                // default literal
                //need help
                if (((IConversionOperation)op).Operand.Kind == OperationKind.Invalid) return true;
            }

            if (k == OperationKind.LocalReference || k == OperationKind.FieldReference || k == OperationKind.PropertyReference)
            {
                //need help: how to get ref-ness from IOperation?
                var parent = op.Syntax.Parent.Kind();
                if (parent == SyntaxKind.RefExpression) return true;
            }

            return k == OperationKind.ObjectCreation
                || k == OperationKind.DefaultValue
                || k == OperationKind.Literal;

            //todo: should return value be OK?
            //todo: move semantics
        }
    }
}
