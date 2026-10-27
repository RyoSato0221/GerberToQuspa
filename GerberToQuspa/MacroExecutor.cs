using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Globalization;

namespace GerberToQuspa
{
    /// <summary>
    /// トークン種類
    /// 数式を分解したときの要素
    /// </summary>
    enum TokenType
    {
        Number,      // 数値 (例: 10, 3.5)
        ConstNumber, // 数値 (*,/の後の数値)
        Variable,    // 変数 (例: $1, $2)
        Operator,    // 演算子 (+ - * /)
        LeftParen,   // (
        RightParen,  // )
        Assign,      // =
        End
    }    

    /// <summary>
    /// トークン
    /// Tokenizerが生成する最小単位
    /// </summary>
    class Token
    {
        public TokenType Type;
        public string Value;
    }

    /// <summary>
    /// 数式ノードのインターフェース
    /// AST（構文木）の基本要素
    /// </summary>
    public interface IExpression
    {
        double Evaluate(Dictionary<int, double> vars);
        IExpression Clone();
        IExpression ExecuteAssignments(Dictionary<int, IExpression> vars);
    }

    /// <summary>
    /// 定数ノード
    /// </summary>
    public class ConstantExpression : IExpression
    {
        private readonly double _value;

        public ConstantExpression(double value)
        {
            _value = value;
        }

        public double Evaluate(Dictionary<int, double> vars)
        {
            return _value;
        }

        public IExpression Clone()
        {
            return this;
        }

        public IExpression ExecuteAssignments(Dictionary<int, IExpression> vars)
        {
            return this;
        }
    }

    /// <summary>
    /// 変数ノード
    /// </summary>
    public class VariableExpression : IExpression
    {
        private readonly int _index;
        public int Index => _index;

        public VariableExpression(int index)
        {
            _index = index;
        }

        public double Evaluate(Dictionary<int, double> vars)
        {
            return vars[_index];
        }

        public IExpression Clone()
        {
            return this;
        }

        public IExpression ExecuteAssignments(Dictionary<int, IExpression> vars)
        {
            if (vars.ContainsKey(Index))
            {
                return vars[Index];
            }
            else
            {
                return this;
            }            
        }
    }

    /// <summary>
    /// 二項演算ノード
    /// 例: A+B , A-B , A*B , A/B
    /// </summary>
    class BinaryExpression : IExpression
    {
        IExpression _left;
        IExpression _right;
        string _op;

        public BinaryExpression(IExpression l, IExpression r, string op)
        {
            _left = l;
            _right = r;
            _op = op;
        }

        public double Evaluate(Dictionary<int, double> vars)
        {
            double a = _left.Evaluate(vars);
            double b = _right.Evaluate(vars);

            switch (_op)
            {
                case "+": return a + b;
                case "-": return a - b;
                case "*":
                case "X":
                case "x":
                    return a * b;
                case "/": return b != 0 ? a / b : 0;
            }

            throw new Exception("Unknown operator");
        }

        public IExpression Clone()
        {
            return this;
        }

        public IExpression ExecuteAssignments(Dictionary<int, IExpression> vars)
        {
            _left = _left.ExecuteAssignments(vars);
            _right = _right.ExecuteAssignments(vars);
            return this;
        }
    }

    class UnaryExpression : IExpression
    {
        IExpression _expr;

        public UnaryExpression(IExpression expr)
        {
            _expr = expr;
        }

        public double Evaluate(Dictionary<int, double> vars)
        {
            return -_expr.Evaluate(vars);
        }

        public IExpression Clone()
        {
            return this;
        }

        public IExpression ExecuteAssignments(Dictionary<int, IExpression> vars)
        {
            return this;
        }
    }

    /// <summary>
    /// 代入式
    /// 例: $1 = $2 + 3
    /// </summary>
    class AssignmentExpression
    {
        public int VariableIndex;
        public IExpression Expression;

        // 実行すると変数辞書を更新する
        public void Execute(Dictionary<int, double> vars)
        {
            vars[VariableIndex] = Expression.Evaluate(vars);
        }
    }

    /// <summary>
    /// Tokenizer
    /// 数式文字列 → Token列へ変換
    /// </summary>
    class Tokenizer
    {
        // Gerber Macro の数式に対応したトークン抽出
        static Regex regex = new Regex(@"\$\d+|-?(?:\d+(?:\.\d+)?|\.\d+)|[+\-*Xx/()]");

        public static List<Token> Tokenize(string expr)
        {
            var tokens = new List<Token>();

            foreach (Match m in regex.Matches(expr))
            {
                string v = m.Value;

                if (v.StartsWith("$"))
                {
                    tokens.Add(new Token
                    {
                        Type = TokenType.Variable,
                        Value = v
                    });
                }
                else if (char.IsDigit(v[0]) || (v.Length > 1 && v[0] == '-' && char.IsDigit(v[1])))
                {
                    if (tokens.Count() != 0)
                    {
                        string last = tokens.Last()?.Value;
                        if (last == "X" || last == "x" || last == "*" || last == "/")
                        {
                            tokens.Add(new Token { Type = TokenType.ConstNumber, Value = v });
                        }
                        else
                        {
                            tokens.Add(new Token { Type = TokenType.Number, Value = v });
                        }
                    }
                    else
                    {
                        tokens.Add(new Token { Type = TokenType.Number, Value = v });
                    }
                }
                else if (v == "(")
                {
                    tokens.Add(new Token { Type = TokenType.LeftParen, Value = v });
                }
                else if (v == ")")
                {
                    tokens.Add(new Token { Type = TokenType.RightParen, Value = v });
                }
                else if (char.IsDigit(v[0]) ||        
                    (v.Length > 1 && v[0] == '-' && (char.IsDigit(v[1]) || v[1] == '.')) ||  
                    (v[0] == '.' && v.Length > 1 && char.IsDigit(v[1])))
                {
                    if (v.StartsWith("."))
                    {
                        v = "0" + v;
                    }
                    else if (v.StartsWith("-."))
                    {
                        v = "-0" + v.Substring(1);
                    }

                    if (tokens.Count != 0)
                    {
                        string last = tokens.Last()?.Value;

                        if (last == "X" || last == "x" || last == "*" || last == "/")
                        {
                            tokens.Add(new Token
                            {
                                Type = TokenType.ConstNumber,
                                Value = v
                            });
                        }
                        else
                        {
                            tokens.Add(new Token
                            {
                                Type = TokenType.Number,
                                Value = v
                            });
                        }
                    }
                    else
                    {
                        tokens.Add(new Token
                        {
                            Type = TokenType.Number,
                            Value = v
                        });
                    }
                }
                else
                {
                    tokens.Add(new Token
                    {
                        Type = TokenType.Operator,
                        Value = v
                    });
                }

            }

            tokens.Add(new Token { Type = TokenType.End });

            return tokens;
        }
    }

    /// <summary>
    /// Parser
    /// Token列 → AST(構文木)へ変換
    /// </summary>
    class Parser
    {
        List<Token> tokens;
        int pos;

        static Dictionary<string, int> precedence = new Dictionary<string, int>
        {
            { "+",1 },
            { "-",1 },
            { "*",2 },
            { "X",2 },
            { "x",2 },
            { "/",2 }
        };

        public Parser(List<Token> t)
        {
            tokens = t;
        }

        Token Peek()
        {
            return tokens[pos];
        }

        Token Next()
        {
            return tokens[pos++];
        }

        public IExpression ParseExpression(CoordinateTransformer ct, int minPrec = 0)
        {
            IExpression left = ParsePrimary(ct);

            while (true)
            {
                Token op = Peek();

                if (op.Type != TokenType.Operator)
                    break;

                int prec = precedence[op.Value];

                if (prec < minPrec)
                    break;

                Next();

                IExpression right = ParseExpression(ct, prec + 1);

                left = new BinaryExpression(left, right, op.Value);
            }

            return left;
        }

        IExpression ParsePrimary(CoordinateTransformer ct)
        {
            Token t = Next();

            if (t.Type == TokenType.Number)
            {
                double value = double.Parse(t.Value, CultureInfo.InvariantCulture);
                return new ConstantExpression(value);
                //decimal value = decimal.Parse(t.Value, CultureInfo.InvariantCulture);
                //long internalValue = ct.ParseRealToInternal(value);
                //return new ConstantExpression(internalValue);
            }

            if (t.Type == TokenType.ConstNumber)
            {
                double value = double.Parse(t.Value, CultureInfo.InvariantCulture);
                return new ConstantExpression(value);
            }

            if (t.Type == TokenType.Variable)
            {
                int index = int.Parse(t.Value.Substring(1));
                return new VariableExpression(index);
            }

            if (t.Type == TokenType.LeftParen)
            {
                var expr = ParseExpression(ct);

                if (Next().Type != TokenType.RightParen)
                    throw new Exception("Missing )");

                return expr;
            }

            if (t.Type == TokenType.Operator && t.Value == "-")
            {
                var expr = ParsePrimary(ct);
                return new UnaryExpression(expr);
            }

            throw new Exception("Invalid expression");
        }
    }

    class ExpressionFactory
    {
        public static IExpression Create(string expr, CoordinateTransformer ct, Dictionary<int, IExpression> vars)
        {
            var tokens = Tokenizer.Tokenize(expr);

            var parser = new Parser(tokens);

            IExpression ex = parser.ParseExpression(ct);
            return ex.ExecuteAssignments(vars);
//            return ex;
        }
    }

    /// <summary>
    /// Macro実行クラス
    /// Macroの逐次評価を担当
    /// </summary>
    public class MacroExecutor
    {
        Dictionary<int, double> variables = new Dictionary<int, double>();

        public void ExecuteLine(string line)
        {
            //var tokens = Tokenizer.Tokenize(line);

            //var parser = new Parser(tokens);

            //var assign = parser.ParseAssignment();

            //assign.Execute(variables);
        }

        public double GetVariable(int index)
        {
            double v;
            if (variables.TryGetValue(index, out v))
                return v;

            return 0;
        }
    }
}