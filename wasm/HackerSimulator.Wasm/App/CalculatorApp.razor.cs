using System;

namespace HackerSimulator.Wasm.Apps
{
    public partial class CalculatorApp : Windows.WindowBase
    {
        private readonly string[][] _layout = new[]
        {
            new[] { "7", "8", "9", "/" },
            new[] { "4", "5", "6", "*" },
            new[] { "1", "2", "3", "-" },
            new[] { "0", ".", "=", "+" },
            new[] { "C" }
        };

        private string _display = "0";
        private double? _accumulator;
        private string? _operator;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "Calculator";
        }

        private void Press(string key)
        {
            if (char.IsDigit(key, 0) || key == ".")
            {
                if (_display == "0" && key != ".")
                    _display = key;
                else
                    _display += key;
                return;
            }

            switch (key)
            {
                case "C":
                    _display = "0";
                    _accumulator = null;
                    _operator = null;
                    break;
                case "=":
                    Calculate();
                    _operator = null;
                    break;
                default:
                    Calculate();
                    _operator = key;
                    break;
            }
        }

        private void Calculate()
        {
            var value = double.TryParse(_display, out var v) ? v : 0;
            if (_accumulator is null)
            {
                _accumulator = value;
            }
            else if (_operator is not null)
            {
                _accumulator = _operator switch
                {
                    "+" => _accumulator + value,
                    "-" => _accumulator - value,
                    "*" => _accumulator * value,
                    "/" => value == 0 ? 0 : _accumulator / value,
                    _ => value
                };
            }

            _display = _accumulator.ToString();
        }
    }
}
