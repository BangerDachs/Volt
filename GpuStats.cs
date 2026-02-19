using System;

namespace Volt
{
    internal sealed class GpuStats
    {
        private readonly double[] _minValues;
        private readonly double[] _maxValues;
        private readonly double[] _avgValues;
        private readonly int[] _sampleCounts;

        public GpuStats(int size)
        {
            _minValues = new double[size];
            _maxValues = new double[size];
            _avgValues = new double[size];
            _sampleCounts = new int[size];
            Reset();
        }

        public void Reset()
        {
            Array.Fill(_minValues, double.MaxValue);
            Array.Fill(_maxValues, double.MinValue);
            Array.Clear(_avgValues);
            Array.Clear(_sampleCounts);
        }

        public void Update(int index, double? value)
        {
            if (!value.HasValue)
                return;

            _sampleCounts[index]++;
            var current = value.Value;
            _minValues[index] = Math.Min(_minValues[index], current);
            _maxValues[index] = Math.Max(_maxValues[index], current);
            _avgValues[index] = ((_avgValues[index] * (_sampleCounts[index] - 1)) + current) / _sampleCounts[index];
        }

        public bool HasSamples(int index) => _sampleCounts[index] > 0;

        public double Min(int index) => _minValues[index];

        public double Max(int index) => _maxValues[index];

        public double Avg(int index) => _avgValues[index];
    }
}
