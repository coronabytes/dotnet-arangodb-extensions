using System;
using System.Linq;

namespace Core.Arango.Linq.Internal
{
    
    
    public class AqlVisitor
    {

        public AqlConvertable Visit(AqlConvertable conv)
        {
            return conv.Accept(this);
        }


        public virtual AqlConvertable VisitPrimitive(AqlPrimitive aqlPrimitive)
        {
            return aqlPrimitive;
        }

        public virtual AqlConvertable VisitVariable(AqlVariable aqlVariable)
        {
            return aqlVariable;
        }

        public virtual AqlConvertable VistConstant(AqlConstant aqlConstant)
        {
            return aqlConstant;
        }

        public virtual AqlConvertable VisitParameter(AqlParameter aqlParameter)
        {
            return aqlParameter;
        }

        public virtual AqlConvertable VisitMemberAccess(AqlMemberAccess aqlMemberAccess)
        {
            var left = Visit(aqlMemberAccess.Left);
            var member = aqlMemberAccess.Member;

            var c = new AqlMemberAccess(left, member);
            if (!aqlMemberAccess.Equals(c))
            {
                return c;
            }

            return aqlMemberAccess;
        }

        public virtual AqlConvertable VisitFunction(AqlFunction aqlFunction)
        {
            var name = aqlFunction.FunctionName;
            
            var c = new AqlFunction(name, aqlFunction.Arguments.Select(x => Visit(x)).ToArray());

            if (!aqlFunction.Equals(c))
            {
                return c;
            }

            return aqlFunction;
        }

        public virtual AqlConvertable VisitInList(AqlInList list)
        {
            var container = Visit(list.Container);
            var element = Visit(list.Element);

            var c = new AqlInList(element, container);
            if (!list.Equals(c))
            {
                return c;
            }

            return list;
        }

        public virtual AqlConvertable VisitBinary(AqlBinary binary)
        {
            var left = Visit(binary.Left);
            var right = Visit(binary.Right);
            var op = binary.OperatorStr;

            var c = new AqlBinary(left,right, op);
            if (!binary.Equals(c))
            {
                return c;
            }

            return binary;
        }

        public virtual AqlConvertable VisitUnary(AqlUnary unary)
        {
            var inner = Visit(unary.Inner);
            var op = unary.OperatorStr;

            var c = new AqlUnary(inner, op);
            if (!unary.Equals(c))
            {
                return c;
            }

            return unary;
        }

        public virtual AqlConvertable VisitGroup(AqlGrouping aqlGrouping)
        {
            var select = VisitSelectBlock(aqlGrouping.SelectBlock);
            var keyProjection = VisitGroupKeyProjection(aqlGrouping.KeyProjection);
            
            var c = new AqlGrouping(keyProjection);
            if (select != null)
            {
                c.SetSelect(select);
            }
            
            if (!aqlGrouping.Equals(c))
            {
                return c;
            }
            
            return aqlGrouping;
        }

        public virtual AqlGroupingKeyProjection VisitGroupKeyProjection(AqlGroupingKeyProjection proj)
        {
            return proj;
        }

        public virtual AqlSimpleSelect VisitSelectBlock(AqlSimpleSelect select)
        {
            var body = Visit(select.Body);
            var parameter = select.Parameter;
            
            var c = new AqlSimpleSelect() {Body =  body, Parameter = parameter};

            if (!select.Equals(c))
            {
                return c;
            }

            return select;
        }
        
        private AqlSort VisitSortBlock(AqlSort sort)
        {
            var body = Visit(sort.Body);
            var parameter = sort.Parameter;
            
            var c = new AqlSort() {Body =  body, Parameter = parameter};

            if (!sort.Equals(c))
            {
                return c;
            }

            return sort;
        }

        public virtual AqlConvertable VisitCollection(AqlCollection aqlCollection)
        {
            var collection = Visit(aqlCollection.Collection);
            var withBrackets = aqlCollection.WithBrackets;
            var select = VisitSelectBlock(aqlCollection.SelectBlock);
            var limit = aqlCollection.Limit;
            var sort = VisitSortBlock(aqlCollection.SortBlock);
            var parameter = aqlCollection.Parameter;
            var grouping = Visit(aqlCollection.Grouping);
            var behaviour = aqlCollection.OutputBehaviour;

            var filters = aqlCollection.FilterBlocks;
            
            // todo implement replacer and equal check
            throw new NotImplementedException();

            return aqlCollection;
        }

        public virtual AqlConvertable VisitConcat(AqlConcat aqlConcat)
        {
            var left = Visit(aqlConcat.Left);
            var right = Visit(aqlConcat.Right);
            var name = aqlConcat.PreferredName;
            
            var c = new AqlConcat(left, right, name);

            if (!aqlConcat.Equals(c))
            {
                return c;
            }

            return aqlConcat;
        }

        public virtual AqlConvertable VisitReturnObjectProjection(AqlReturnObjectProjection proj)
        {
            var c = new AqlReturnObjectProjection();

            foreach (var m in proj.MemberDict)
            {
                c.AddMember(m.Key, Visit(m.Value));
            }

            if (!proj.Equals(c))
            {
                return c;
            }

            return proj;

        }
    }
}