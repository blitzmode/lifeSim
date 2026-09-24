using System;
using System.Numerics;
using NAudio.Wave;

class Program
{
    static void Main()
    {
        AudioAnalyzer analyzer = new AudioAnalyzer();

        analyzer.Start();

        Console.WriteLine("Listening...");
        Console.WriteLine("Press ENTER to stop.");
        Console.ReadLine();

        analyzer.Stop();
    }
}


// ============================================================
// AudioAnalyzer
//
// Continuously receives microphone samples and analyses them.
//
// The important idea is:
//
// microphone
//     ↓
// raw samples
//     ↓
// rolling buffer
//     ↓
// raw-wave analysis
//     ↓
// FFT
//     ↓
// frequency analysis
//     ↓
// AudioAnalysis
// ============================================================

class AudioAnalyzer
{
    // --------------------------------------------------------
    // Audio settings
    // --------------------------------------------------------

    public const int SampleRate = 44100;

    // Number of samples used for each analysis.
    //
    // 4096 samples at 44100 Hz is about 93 ms.
    public const int WindowSize = 4096;

    // How many new samples arrive before we perform another
    // analysis.
    //
    // 512 samples = about 11.6 ms.
    public const int HopSize = 512;


    // --------------------------------------------------------
    // Microphone
    // --------------------------------------------------------

    private WaveInEvent microphone;


    // --------------------------------------------------------
    // Rolling sample buffer
    //
    // This always contains the most recent WindowSize samples.
    // --------------------------------------------------------

    private float[] sampleBuffer = new float[WindowSize];

    private int samplesSinceLastAnalysis = 0;


    // Used to detect zero crossings.
    private float previousSample = 0;


    // Used for comparing the current FFT to the previous FFT.
    private float[] previousSpectrum;


    public AudioAnalyzer()
    {
        previousSpectrum = new float[WindowSize / 2];
    }


    // ========================================================
    // START
    // ========================================================

    public void Start()
    {
        microphone = new WaveInEvent();

        microphone.WaveFormat =
            new WaveFormat(SampleRate, 16, 1);

        microphone.DataAvailable += Microphone_DataAvailable;

        microphone.StartRecording();
    }


    // ========================================================
    // STOP
    // ========================================================

    public void Stop()
    {
        if (microphone != null)
        {
            microphone.StopRecording();
            microphone.Dispose();
            microphone = null;
        }
    }


    // ========================================================
    // MICROPHONE CALLBACK
    //
    // NAudio gives us new microphone samples here.
    // ========================================================

    private void Microphone_DataAvailable(
        object? sender,
        WaveInEventArgs e)
    {
        for (int i = 0; i < e.BytesRecorded; i += 2)
        {
            // Microphone is 16-bit PCM.
            short rawSample =
                BitConverter.ToInt16(e.Buffer, i);

            // Convert -32768..32767 into approximately
            // -1..1.
            float sample = rawSample / 32768f;

            AddSample(sample);
        }
    }


    // ========================================================
    // ADD SAMPLE
    //
    // Add one new sample to the rolling buffer.
    // ========================================================

    private void AddSample(float sample)
    {
        // Shift everything one position left.
        //
        // This is intentionally simple rather than fast.
        for (int i = 0; i < WindowSize - 1; i++)
        {
            sampleBuffer[i] = sampleBuffer[i + 1];
        }

        sampleBuffer[WindowSize - 1] = sample;

        samplesSinceLastAnalysis++;

        // Detect zero crossing.
        DetectZeroCrossing(sample);

        previousSample = sample;


        // Once enough new samples have arrived,
        // analyse the latest window.
        if (samplesSinceLastAnalysis >= HopSize)
        {
            samplesSinceLastAnalysis = 0;

            AnalyzeCurrentWindow();
        }
    }


    // ========================================================
    // ANALYSE CURRENT WINDOW
    // ========================================================

    private void AnalyzeCurrentWindow()
    {
        AudioAnalysis result = new AudioAnalysis();


        // ----------------------------------------------------
        // RAW WAVEFORM INFORMATION
        // ----------------------------------------------------

        result.RMS = CalculateRMS();

        result.PeakAmplitude =
            CalculatePeakAmplitude();

        result.ZeroCrossingRate =
            CalculateZeroCrossingRate();


        // ----------------------------------------------------
        // AUTOCORRELATION
        //
        // Used to estimate periodicity / pitch.
        // ----------------------------------------------------

        result.AutocorrelationPitch =
            CalculatePitchUsingAutocorrelation();


        // ----------------------------------------------------
        // FFT
        // ----------------------------------------------------

        float[] spectrum = CalculateFFT();


        result.Spectrum = spectrum;


        // ----------------------------------------------------
        // FREQUENCY FEATURES
        // ----------------------------------------------------

        result.SpectralCentroid =
            CalculateSpectralCentroid(spectrum);

        result.SpectralBandwidth =
            CalculateSpectralBandwidth(
                spectrum,
                result.SpectralCentroid);

        result.SpectralRolloff =
            CalculateSpectralRolloff(spectrum);


        // ----------------------------------------------------
        // SPECTRAL FLUX
        //
        // How much has the spectrum changed since the
        // previous analysis?
        // ----------------------------------------------------

        result.SpectralFlux =
            CalculateSpectralFlux(
                spectrum,
                previousSpectrum);


        // Save spectrum for next time.
        previousSpectrum = spectrum;


        // ----------------------------------------------------
        // FIND FREQUENCY PEAKS
        // ----------------------------------------------------

        result.Peaks =
            FindFrequencyPeaks(spectrum);


        // ----------------------------------------------------
        // ESTIMATE FUNDAMENTAL FREQUENCY
        // ----------------------------------------------------

        result.FundamentalFrequency =
            EstimateFundamentalFrequency(
                spectrum,
                result.Peaks);


        // ----------------------------------------------------
        // HARMONICS
        // ----------------------------------------------------

        result.Harmonics =
            FindHarmonics(
                spectrum,
                result.FundamentalFrequency);


        // ----------------------------------------------------
        // ONSET
        // ----------------------------------------------------

        result.Onset =
            result.SpectralFlux > 5.0f;


        // ----------------------------------------------------
        // PRINT RESULT
        // ----------------------------------------------------

        PrintAnalysis(result);
    }


    // ========================================================
    // RMS
    //
    // Measures overall signal energy.
    // ========================================================

    private float CalculateRMS()
    {
        double sum = 0;

        foreach (float sample in sampleBuffer)
        {
            sum += sample * sample;
        }

        return (float)Math.Sqrt(
            sum / sampleBuffer.Length);
    }


    // ========================================================
    // PEAK AMPLITUDE
    // ========================================================

    private float CalculatePeakAmplitude()
    {
        float largest = 0;

        foreach (float sample in sampleBuffer)
        {
            float magnitude = Math.Abs(sample);

            if (magnitude > largest)
                largest = magnitude;
        }

        return largest;
    }


    // ========================================================
    // ZERO CROSSING RATE
    // ========================================================

    private float CalculateZeroCrossingRate()
    {
        int crossings = 0;

        for (int i = 1; i < sampleBuffer.Length; i++)
        {
            bool wasPositive =
                sampleBuffer[i - 1] >= 0;

            bool isPositive =
                sampleBuffer[i] >= 0;

            if (wasPositive != isPositive)
                crossings++;
        }

        return (float)crossings / sampleBuffer.Length;
    }


    // ========================================================
    // ZERO CROSSING DETECTION
    //
    // This demonstrates that zero crossings can also be
    // detected continuously as samples arrive.
    // ========================================================

    private void DetectZeroCrossing(float sample)
    {
        bool previousPositive =
            previousSample >= 0;

        bool currentPositive =
            sample >= 0;

        if (previousPositive != currentPositive)
        {
            // A zero crossing occurred.
            //
            // We don't currently need to do anything with it
            // because CalculateZeroCrossingRate() examines the
            // entire current window.
        }
    }


    // ========================================================
    // AUTOCORRELATION PITCH
    //
    // Finds how long it takes for the waveform to repeat.
    // ========================================================

    private float CalculatePitchUsingAutocorrelation()
    {
        int minimumFrequency = 50;
        int maximumFrequency = 1000;

        int minimumLag =
            SampleRate / maximumFrequency;

        int maximumLag =
            SampleRate / minimumFrequency;


        float bestCorrelation = 0;
        int bestLag = 0;


        for (int lag = minimumLag;
             lag <= maximumLag;
             lag++)
        {
            double correlation = 0;

            for (int i = 0;
                 i < WindowSize - lag;
                 i++)
            {
                correlation +=
                    sampleBuffer[i] *
                    sampleBuffer[i + lag];
            }


            if (correlation > bestCorrelation)
            {
                bestCorrelation = (float)correlation;
                bestLag = lag;
            }
        }


        if (bestLag == 0)
            return 0;


        return SampleRate / (float)bestLag;
    }


    // ========================================================
    // FFT
    // ========================================================

    private float[] CalculateFFT()
    {
        Complex[] data =
            new Complex[WindowSize];


        // Copy samples into FFT input.
        for (int i = 0; i < WindowSize; i++)
        {
            // Hann window.
            //
            // This reduces discontinuities at the edges.
            double window =
                0.5 *
                (1 -
                Math.Cos(
                    2 * Math.PI * i /
                    (WindowSize - 1)));

            data[i] =
                sampleBuffer[i] * window;
        }


        PerformFFT(data);


        // Only half of the FFT contains useful positive
        // frequencies.
        float[] spectrum =
            new float[WindowSize / 2];


        for (int i = 0; i < spectrum.Length; i++)
        {
            spectrum[i] =
                (float)data[i].Magnitude;
        }


        return spectrum;
    }


    // ========================================================
    // FFT IMPLEMENTATION
    //
    // This is here so you can see what the FFT actually does.
    // ========================================================

    private void PerformFFT(Complex[] data)
    {
        int n = data.Length;


        // Bit-reversal permutation.
        int j = 0;

        for (int i = 1; i < n; i++)
        {
            int bit = n >> 1;

            while ((j & bit) != 0)
            {
                j ^= bit;
                bit >>= 1;
            }

            j ^= bit;

            if (i < j)
            {
                Complex temp = data[i];

                data[i] = data[j];
                data[j] = temp;
            }
        }


        // FFT stages.
        for (int length = 2;
             length <= n;
             length *= 2)
        {
            double angle =
                -2 * Math.PI / length;

            Complex rotation =
                Complex.FromPolarCoordinates(
                    1,
                    angle);


            for (int start = 0;
                 start < n;
                 start += length)
            {
                Complex multiplier = 1;


                for (int i = 0;
                     i < length / 2;
                     i++)
                {
                    Complex even =
                        data[start + i];

                    Complex odd =
                        data[
                            start +
                            i +
                            length / 2] *
                        multiplier;


                    data[start + i] =
                        even + odd;

                    data[
                        start +
                        i +
                        length / 2] =
                        even - odd;


                    multiplier *= rotation;
                }
            }
        }
    }


    // ========================================================
    // SPECTRAL CENTROID
    // ========================================================

    private float CalculateSpectralCentroid(
        float[] spectrum)
    {
        double weightedSum = 0;
        double totalEnergy = 0;


        for (int i = 0;
             i < spectrum.Length;
             i++)
        {
            float frequency =
                i * SampleRate /
                (float)WindowSize;

            float magnitude =
                spectrum[i];


            weightedSum +=
                frequency * magnitude;

            totalEnergy += magnitude;
        }


        if (totalEnergy == 0)
            return 0;


        return (float)(
            weightedSum /
            totalEnergy);
    }


    // ========================================================
    // SPECTRAL BANDWIDTH
    // ========================================================

    private float CalculateSpectralBandwidth(
        float[] spectrum,
        float centroid)
    {
        double sum = 0;
        double energy = 0;


        for (int i = 0;
             i < spectrum.Length;
             i++)
        {
            float frequency =
                i * SampleRate /
                (float)WindowSize;

            float magnitude =
                spectrum[i];


            double difference =
                frequency - centroid;


            sum +=
                difference *
                difference *
                magnitude;

            energy += magnitude;
        }


        if (energy == 0)
            return 0;


        return (float)Math.Sqrt(
            sum / energy);
    }


    // ========================================================
    // SPECTRAL ROLLOFF
    // ========================================================

    private float CalculateSpectralRolloff(
        float[] spectrum)
    {
        double total = 0;

        foreach (float value in spectrum)
            total += value;


        double target =
            total * 0.85;


        double running = 0;


        for (int i = 0;
             i < spectrum.Length;
             i++)
        {
            running += spectrum[i];

            if (running >= target)
            {
                return i *
                    SampleRate /
                    (float)WindowSize;
            }
        }


        return 0;
    }


    // ========================================================
    // SPECTRAL FLUX
    // ========================================================

    private float CalculateSpectralFlux(
        float[] current,
        float[] previous)
    {
        double difference = 0;


        for (int i = 0;
             i < current.Length;
             i++)
        {
            float change =
                current[i] -
                previous[i];


            // Only count increases.
            //
            // This makes the measurement particularly useful
            // for detecting new sound appearing.
            if (change > 0)
            {
                difference +=
                    change * change;
            }
        }


        return (float)Math.Sqrt(difference);
    }


    // ========================================================
    // FREQUENCY PEAKS
    // ========================================================

    private List<FrequencyPeak> FindFrequencyPeaks(
        float[] spectrum)
    {
        List<FrequencyPeak> peaks =
            new List<FrequencyPeak>();


        for (int i = 1;
             i < spectrum.Length - 1;
             i++)
        {
            if (spectrum[i] >
                spectrum[i - 1] &&
                spectrum[i] >
                spectrum[i + 1])
            {
                float frequency =
                    i * SampleRate /
                    (float)WindowSize;


                peaks.Add(
                    new FrequencyPeak
                    {
                        Frequency = frequency,
                        Magnitude = spectrum[i]
                    });
            }
        }


        return peaks;
    }


    // ========================================================
    // FUNDAMENTAL FREQUENCY
    //
    // Looks for a low frequency which explains several
    // harmonics.
    // ========================================================

    private float EstimateFundamentalFrequency(
        float[] spectrum,
        List<FrequencyPeak> peaks)
    {
        float bestFrequency = 0;
        int bestScore = 0;


        foreach (FrequencyPeak candidate in peaks)
        {
            float frequency =
                candidate.Frequency;


            // Ignore frequencies outside our useful pitch range.
            if (frequency < 50 ||
                frequency > 1000)
                continue;


            int score = 0;


            // Look for harmonics:
            //
            // F
            // 2F
            // 3F
            // 4F
            // ...
            for (int harmonic = 1;
                 harmonic <= 6;
                 harmonic++)
            {
                float expected =
                    frequency * harmonic;


                float tolerance =
                    expected * 0.03f;


                foreach (FrequencyPeak peak in peaks)
                {
                    if (Math.Abs(
                        peak.Frequency -
                        expected) < tolerance)
                    {
                        score++;
                        break;
                    }
                }
            }


            if (score > bestScore)
            {
                bestScore = score;
                bestFrequency = frequency;
            }
        }


        return bestFrequency;
    }


    // ========================================================
    // HARMONICS
    // ========================================================

    private List<Harmonic> FindHarmonics(
        float[] spectrum,
        float fundamental)
    {
        List<Harmonic> harmonics =
            new List<Harmonic>();


        if (fundamental <= 0)
            return harmonics;


        for (int number = 1;
             number <= 10;
             number++)
        {
            float expectedFrequency =
                fundamental * number;


            int bin =
                (int)Math.Round(
                    expectedFrequency *
                    WindowSize /
                    SampleRate);


            if (bin < 0 ||
                bin >= spectrum.Length)
                continue;


            harmonics.Add(
                new Harmonic
                {
                    Number = number,

                    Frequency =
                        expectedFrequency,

                    Magnitude =
                        spectrum[bin]
                });
        }


        return harmonics;
    }


    // ========================================================
    // PRINT RESULTS
    // ========================================================

    private void PrintAnalysis(
        AudioAnalysis a)
    {
        Console.Clear();


        Console.WriteLine(
            "================ AUDIO BRAIN ================");


        Console.WriteLine(
            $"RMS:                {a.RMS:F4}");

        Console.WriteLine(
            $"Peak:               {a.PeakAmplitude:F4}");

        Console.WriteLine(
            $"Zero crossing rate: {a.ZeroCrossingRate:F4}");

        Console.WriteLine();


        Console.WriteLine(
            $"Autocorrelation F0: {a.AutocorrelationPitch:F1} Hz");

        Console.WriteLine(
            $"FFT F0:             {a.FundamentalFrequency:F1} Hz");


        Console.WriteLine();


        Console.WriteLine(
            $"Centroid:           {a.SpectralCentroid:F1} Hz");

        Console.WriteLine(
            $"Bandwidth:          {a.SpectralBandwidth:F1} Hz");

        Console.WriteLine(
            $"Rolloff:            {a.SpectralRolloff:F1} Hz");

        Console.WriteLine(
            $"Spectral flux:      {a.SpectralFlux:F3}");


        Console.WriteLine();


        Console.WriteLine(
            $"ONSET:              {a.Onset}");


        Console.WriteLine();


        Console.WriteLine(
            "Harmonics:");

        foreach (Harmonic harmonic in a.Harmonics)
        {
            Console.WriteLine(
                $"  {harmonic.Number}: " +
                $"{harmonic.Frequency:F1} Hz   " +
                $"strength {harmonic.Magnitude:F3}");
        }
    }
}


// ============================================================
// DATA CLASSES
// ============================================================

class AudioAnalysis
{
    public float RMS;

    public float PeakAmplitude;

    public float ZeroCrossingRate;

    public float AutocorrelationPitch;

    public float FundamentalFrequency;

    public float SpectralCentroid;

    public float SpectralBandwidth;

    public float SpectralRolloff;

    public float SpectralFlux;

    public bool Onset;

    public float[] Spectrum =
        Array.Empty<float>();

    public List<FrequencyPeak> Peaks =
        new List<FrequencyPeak>();

    public List<Harmonic> Harmonics =
        new List<Harmonic>();
}


class FrequencyPeak
{
    public float Frequency;

    public float Magnitude;
}


class Harmonic
{
    public int Number;

    public float Frequency;

    public float Magnitude;
}