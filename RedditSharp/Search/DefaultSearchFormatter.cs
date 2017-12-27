﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RedditSharp.Search
{
    public class DefaultSearchFormatter : ISearchFormatter
    {
        string ISearchFormatter.Format(Expression<Func<AdvancedSearchFilter, bool>> search)
        {
            Expression expression = null;
            Stack<Expression> expressionStack = new Stack<Expression>();
            Stack<FormatInfo> formatInfoStack = new Stack<FormatInfo>();
            expressionStack.Push(search.Body);
            Stack<string> searchStack = new Stack<string>();
            while (expressionStack.Count > 0) {
                expression = expressionStack.Pop();
                switch (expression)
                {
                    case MemberExpression memberExpression:
                        MemberExpressionHelper(memberExpression, searchStack, formatInfoStack);
                        break;
                    case UnaryExpression unaryExpression:
                        UnaryExpressionHelper(unaryExpression, expressionStack, formatInfoStack);
                        break;
                    case BinaryExpression binaryExpression:
                        BinaryExpressionHelper(binaryExpression, expressionStack, formatInfoStack);
                        break;
                    case ConstantExpression constantExpresssion:
                        searchStack.Push(ConstantExpressionHelper(constantExpresssion));
                        break;
                    default:
                        throw new NotImplementedException(expression.ToString());
                }
            }

            string searchQuery = string.Empty;
            Stack<string> compoundSearchStack = new Stack<string>();
            while(formatInfoStack.Count >0)
            {
                FormatInfo current = formatInfoStack.Pop();
                string[] formatParameters = new string[current.ParameterCount];
                int currentCount = current.ParameterCount;
                while(currentCount > 0)
                {
                    formatParameters[formatParameters.Length - currentCount] = current.IsCompound  ? compoundSearchStack.Pop() : searchStack.Pop();
                    currentCount--;
                }
               
                compoundSearchStack.Push(string.Format(current.Pattern, formatParameters));
                
            }

            return compoundSearchStack.Pop();
        }

        private string ConstantExpressionHelper(ConstantExpression constantExpresssion)
        {
            return constantExpresssion.ToString().Replace("\"","");
        }

        private void BinaryExpressionHelper(BinaryExpression expression, Stack<Expression> expressionStack, Stack<FormatInfo> formatInfoStack)
        {
            if(IsAdvancedSearchMemberExpression(expression.Left) && IsAdvancedSearchMemberExpression(expression.Right))
            {
                throw new InvalidOperationException("Cannot filter by comparing to fields.");
            }
            else if(IsAdvancedSearchMemberExpression(expression.Right))
            {
                expressionStack.Push(expression.Left);
                expressionStack.Push(expression.Right);
            }
            else
            {
                expressionStack.Push(expression.Right);
                expressionStack.Push(expression.Left);
            }


            if (expression.NodeType != ExpressionType.Equal)
            {
                formatInfoStack.Push(expression.ToFormatInfo());
                //searchStack.Push("NOT(+{0}+)");
            }
            

        }

     
        private void UnaryExpressionHelper(UnaryExpression expression, Stack<Expression> expressionStack,Stack<FormatInfo> formatInfoStack)
        {
            formatInfoStack.Push( expression.ToFormatInfo());
            expressionStack.Push(expression.Operand);
            //return expressionOperator;
        }

        private void MemberExpressionHelper(MemberExpression expression, Stack<string> searchStack, Stack<FormatInfo> formatInfoStack)
        {
            MemberInfo member = expression.Member;
            

            if (member.DeclaringType == typeof(AdvancedSearchFilter))
            {
                string result = member.Name.Replace(BOOL_PROPERTY_PREFIX, string.Empty).ToLower();
                formatInfoStack.Push(expression.ToFormatInfo());
                searchStack.Push(result);
                if (expression.Type == typeof(bool))
                {
                    searchStack.Push("1");
                    
                }
            }
        }


        



        private const string BOOL_PROPERTY_PREFIX = "Is";


        private static readonly List<ExpressionType> conditionalTypes = new List<ExpressionType>()
        {
            ExpressionType.AndAlso,
            ExpressionType.And,
            ExpressionType.OrElse,
            ExpressionType.Or
        };

        private static readonly List<ExpressionType> evaluateExpressions = new List<ExpressionType>()
        {
            ExpressionType.Add,
            ExpressionType.Subtract,
            ExpressionType.Multiply,
            ExpressionType.Divide,
            ExpressionType.Coalesce,
            ExpressionType.Conditional
        };

        private static bool IsAdvancedSearchMemberExpression(Expression expression)
        {
            MemberExpression memberExpression = expression as MemberExpression;
            return memberExpression?.Member.DeclaringType == typeof(AdvancedSearchFilter);
        }

        internal class FormatInfo
        {
            public string Pattern { get; private set; }
            public int ParameterCount { get; private set; }
            public bool IsCompound { get; private set; }

            public FormatInfo(string pattern, int parameterCount = 0, bool isCompound = false)
            {
                Pattern = pattern;
                ParameterCount = parameterCount;
                IsCompound = isCompound;
            }

            internal static FormatInfo Not = new FormatInfo("NOT(+{0}+)", 1, true);
            internal static FormatInfo NotEqual = Not;
            internal static FormatInfo AndAlso = new FormatInfo("(+{0}+AND+{1}+)", 2, true);
            internal static FormatInfo OrElse = new FormatInfo("(+{0}+OR+{1}+)", 2, true);
            internal static FormatInfo MemberAccess = new FormatInfo("{1}:{0}", 2);
        }
    }

    public static class Extensions
    {
        internal static DefaultSearchFormatter.FormatInfo ToFormatInfo(this Expression expression)
        {
            ExpressionType? type = expression?.NodeType;
            switch (type)
            {
                case ExpressionType.Not:
                case ExpressionType.NotEqual:
                    return DefaultSearchFormatter.FormatInfo.Not;
                case ExpressionType.Equal:
                    throw new NotImplementedException("Currently not supporting Equal expression.");
                case ExpressionType.AndAlso:
                    return DefaultSearchFormatter.FormatInfo.AndAlso;
                case ExpressionType.MemberAccess:
                    return DefaultSearchFormatter.FormatInfo.MemberAccess;
                case ExpressionType.OrElse:
                    return DefaultSearchFormatter.FormatInfo.OrElse;
            }
            throw new NotImplementedException($"{type.ToString()} is not implemented.");
        }

    }
}
