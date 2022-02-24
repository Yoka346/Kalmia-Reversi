//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Kalmia.Reversi
//{
//    internal class LinkedSquareListNode
//    {
//        public LinkedSquareListNode Prev { get; set; }
//        public LinkedSquareListNode Next { get; set; }
//        public BoardPosition Pos;

//        public LinkedSquareListNode(BoardPosition pos, LinkedSquareListNode prev, LinkedSquareListNode next)
//        {
//            this.Pos = pos;
//            this.Prev = prev;
//            this.Next = next;
//        }
//    }

//    /// <summary>
//    /// Provides board square linked list.
//    /// </summary>
//    internal class LinkedSquareList
//    {
//        LinkedSquareListNode sentinel;

//        public int Count { get; private set; }

//        public LinkedSquareListNode First { get { return this.sentinel.Next; } }
//        public LinkedSquareListNode Last { get { return this.sentinel.Prev; }  }

//        public LinkedSquareList(FastBoard board)
//        {
//            this.sentinel = new LinkedSquareListNode(BoardPosition.Null, null, null);
//            this.sentinel.Next = this.sentinel;
//            this.sentinel.Prev = this.sentinel;

//            foreach(var pos in board.EnumerateEmptySquares())
//                InsertFirst(pos);
//        }

//        public LinkedSquareListNode InsertFirst(BoardPosition pos)
//        {
//            return InsertAfter(this.sentinel, pos);
//        }

//        public LinkedSquareListNode InsertLast(BoardPosition pos)
//        {
//            return InsertBefore(this.sentinel, pos);
//        }

//        public LinkedSquareListNode InsertAfter(LinkedSquareListNode node, BoardPosition pos)
//        {
//            var n = new LinkedSquareListNode(pos, node, node.Next);
//            node.Next.Prev = n;
//            node.Next = n;
//            this.Count++;
//            return n;
//        }

//        public LinkedSquareListNode InsertBefore(LinkedSquareListNode node, BoardPosition pos)
//        {
//            var n = new LinkedSquareListNode(pos, node.Prev, node);
//            node.Prev.Next = n;
//            node.Prev = n;
//            this.Count++;
//            return n;
//        }

//        public LinkedSquareListNode Remove(LinkedSquareListNode node)
//        {
//            node.Prev.Next = node.Next;
//            node.Next.Prev = node.Prev;
//            this.Count--;
//            return node.Next;
//        }
//    }
//}
