using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Apps
{
    [AppIcon("fa:calculator")]
    public partial class CalculatorApp : Windows.WindowBase
    {
        private string _expression = string.Empty;
        private ElementReference _inputRef;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "Calculator";
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (firstRender)
            {
                await _inputRef.FocusAsync();
            }
        }

        private void HandleKey(KeyboardEventArgs e)
        {
            if (e.Key == "=" || e.Key == "Enter")
            {
                if (_expression.EndsWith("="))
                    _expression = _expression[..^1];

                try
                {
                    var result = Evaluate(_expression);
                    _expression = result.ToString();
                }
                catch
                {
                    _expression = "Error";
                }
            }
        }

        private static double Evaluate(string expression)
        {
            int index = 0;

            double ParseExpression()
            {
                double value = ParseTerm();
                while (true)
                {
                    SkipWhite();
                    if (index >= expression.Length)
                        break;
                    char op = expression[index];
                    if (op != '+' && op != '-')
                        break;
                    index++;
                    double term = ParseTerm();
                    value = op == '+' ? value + term : value - term;
                }
                return value;
            }

            double ParseTerm()
            {
                double value = ParseFactor();
                while (true)
                {
                    SkipWhite();
                    if (index >= expression.Length)
                        break;
                    char op = expression[index];
                    if (op != '*' && op != '/')
                        break;
                    index++;
                    double factor = ParseFactor();
                    value = op == '*' ? value * factor : value / factor;
                }
                return value;
            }

            double ParseFactor()
            {
                SkipWhite();
                double sign = 1;
                while (index < expression.Length && (expression[index] == '+' || expression[index] == '-'))
                {
                    if (expression[index] == '-')
                        sign = -sign;
                    index++;
                    SkipWhite();
                }

                if (index < expression.Length && expression[index] == '(')
                {
                    index++;
                    double val = ParseExpression();
                    SkipWhite();
                    if (index < expression.Length && expression[index] == ')')
                        index++;
                    return sign * val;
                }

                int start = index;
                while (index < expression.Length && (char.IsDigit(expression[index]) || expression[index] == '.'))
                    index++;
                var numberStr = expression[start..index];
                double.TryParse(numberStr, out var valNumber);
                return sign * valNumber;
            }

            void SkipWhite()
            {
                while (index < expression.Length && char.IsWhiteSpace(expression[index]))
                    index++;
            }

            var result = ParseExpression();
            SkipWhite();
            if (index < expression.Length)
                throw new FormatException("Invalid expression");
            return result;
        }
    }
}
