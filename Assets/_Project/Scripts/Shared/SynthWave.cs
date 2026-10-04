namespace CatapultGames
{
    // Each sound is a sum of a few voices (pitched tones and filtered noise)
    // under simple attack / exponential-decay envelopes, normalised and
    // soft-clipped. Built once per hub, a few milliseconds in all.
    internal enum SynthWave { Sine, Triangle, Soft }   // Soft = rounded square, for beeps
}
