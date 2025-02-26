﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DSharpPlus.VoiceNext.Codec;
using DSharpPlus.VoiceNext.Entities;

namespace DSharpPlus.VoiceNext
{
    /// <summary>
    /// Stream used to transmit audio data via <see cref="VoiceNextConnection"/>.
    /// </summary>
    public sealed class VoiceTransmitStream : Stream
    {
        /// <summary>
        /// Gets whether this stream can be read from. Always false.
        /// </summary>
        public override bool CanRead => false;

        /// <summary>
        /// Gets whether this stream can be seeked. Always false.
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// Gets whether this stream can be written to. Always false.
        /// </summary>
        public override bool CanWrite => true;

        /// <summary>
        /// Gets the length of the data in this stream. Always throws <see cref="InvalidOperationException"/>.
        /// </summary>
        public override long Length => throw new InvalidOperationException($"{GetType()} cannot have a length.");

        /// <summary>
        /// Gets the position in this stream. Always throws <see cref="InvalidOperationException"/>.
        /// </summary>
        public override long Position
        {
            get => throw new InvalidOperationException($"Cannot get position of {GetType()}.");
            set => throw new InvalidOperationException($"Cannot seek {GetType()}.");
        }

        /// <summary>
        /// Gets the PCM sample duration for this stream.
        /// </summary>
        public int SampleDuration
            => PcmBufferDuration;

        /// <summary>
        /// Gets or sets the volume modifier for this stream. Changing this will alter the volume of the output. 1.0 is 100%.
        /// </summary>
        public double VolumeModifier
        {
            get => _volume;
            set
            {
                if (value < 0 || value > 2.5)
                    throw new ArgumentOutOfRangeException(nameof(value), "Volume needs to be between 0% and 250%.");

                _volume = value;
            }
        }
        private double _volume = 1.0;

        private VoiceNextConnection Connection { get; }
        private int PcmBufferDuration { get; }
        private byte[] PcmBuffer { get; }
        private Memory<byte> PcmMemory { get; }
        private int PcmBufferLength { get; set; }

        private List<IVoiceFilter> Filters { get; }

        internal VoiceTransmitStream(VoiceNextConnection vnc, int pcmBufferDuration)
        {
            Connection = vnc;
            PcmBufferDuration = pcmBufferDuration;
            PcmBuffer = new byte[vnc.AudioFormat.CalculateSampleSize(pcmBufferDuration)];
            PcmMemory = PcmBuffer.AsMemory();
            PcmBufferLength = 0;
            Filters = new List<IVoiceFilter>();
        }

        /// <summary>
        /// Reads from the stream. Throws <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <param name="buffer">Buffer to read to.</param>
        /// <param name="offset">Offset to read to.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <returns>Number of bytes read.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException($"Cannot read from {GetType()}.");
        }

        /// <summary>
        /// Seeks the stream. Throws <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <param name="offset">Offset to seek to.</param>
        /// <param name="origin">Origin of seeking.</param>
        /// <returns>New position in the stream.</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException($"Cannot seek {GetType()}.");
        }

        /// <summary>
        /// Sets length of this stream. Throws <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <param name="value">Length to set.</param>
        public override void SetLength(long value)
        {
            throw new InvalidOperationException($"Cannot set length of {GetType()}.");
        }

        /// <summary>
        /// Writes PCM data to the stream. The data is prepared for transmission, and enqueued.
        /// </summary>
        /// <param name="buffer">PCM data buffer to send.</param>
        /// <param name="offset">Start of the data in the buffer.</param>
        /// <param name="count">Number of bytes from the buffer.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (PcmBuffer)
            {
                var remaining = count;
                var buffSpan = buffer.AsSpan().Slice(offset, count);
                var pcmSpan = PcmMemory.Span;

                while (remaining > 0)
                {
                    var len = Math.Min(pcmSpan.Length - PcmBufferLength, remaining);

                    var tgt = pcmSpan.Slice(PcmBufferLength);
                    var src = buffSpan.Slice(0, len);

                    src.CopyTo(tgt);
                    PcmBufferLength += len;
                    remaining -= len;
                    buffSpan = buffSpan.Slice(len);

                    if (PcmBufferLength == PcmBuffer.Length)
                    {
                        var pcm16 = MemoryMarshal.Cast<byte, short>(pcmSpan);

                        // pass through any filters, if applicable
                        lock (Filters)
                        {
                            if (Filters.Any())
                            {
                                foreach (var filter in Filters)
                                    filter.Transform(pcm16, Connection.AudioFormat, SampleDuration);
                            }
                        }

                        if (VolumeModifier != 1)
                        {
                            // alter volume
                            for (var i = 0; i < pcm16.Length; i++)
                                pcm16[i] = (short)(pcm16[i] * VolumeModifier);
                        }

                        PcmBufferLength = 0;
                        var packet = new byte[pcmSpan.Length];
                        var packetMemory = packet.AsMemory();
                        Connection.PreparePacket(pcmSpan, ref packetMemory);
                        Connection.EnqueuePacket(new VoicePacket(packetMemory, PcmBufferDuration));
                    }
                }
            }
        }

        /// <summary>
        /// Flushes the rest of the PCM data in this buffer to VoiceNext packet queue.
        /// </summary>
        public override void Flush()
        {
            var pcm = PcmMemory.Span;
            Helpers.ZeroFill(pcm.Slice(PcmBufferLength));

            var pcm16 = MemoryMarshal.Cast<byte, short>(pcm);

            // pass through any filters, if applicable
            lock (Filters)
            {
                if (Filters.Any())
                {
                    foreach (var filter in Filters)
                        filter.Transform(pcm16, Connection.AudioFormat, SampleDuration);
                }
            }

            if (VolumeModifier != 1)
            {
                // alter volume
                for (var i = 0; i < pcm16.Length; i++)
                    pcm16[i] = (short)(pcm16[i] * VolumeModifier);
            }

            var packet = new byte[pcm.Length];
            var packetMemory = packet.AsMemory();
            Connection.PreparePacket(pcm, ref packetMemory);
            Connection.EnqueuePacket(new VoicePacket(packetMemory, PcmBufferDuration));
        }

        /// <summary>
        /// Gets the collection of installed PCM filters, in order of their execution.
        /// </summary>
        /// <returns>Installed PCM filters, in order of execution.</returns>
        public IEnumerable<IVoiceFilter> GetInstalledFilters()
        {
            foreach (var filter in Filters)
                yield return filter;
        }

        /// <summary>
        /// Installs a new PCM filter, with specified execution order.
        /// </summary>
        /// <param name="filter">Filter to install.</param>
        /// <param name="order">Order of the new filter. This determines where the filter will be inserted in the filter stream.</param>
        public void InstallFilter(IVoiceFilter filter, int order = int.MaxValue)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order), "Filter order must be greater than or equal to 0.");

            lock (Filters)
            {
                var filters = Filters;
                if (order >= filters.Count)
                    filters.Add(filter);
                else
                    filters.Insert(order, filter);
            }
        }

        /// <summary>
        /// Uninstalls an installed PCM filter.
        /// </summary>
        /// <param name="filter">Filter to uninstall.</param>
        /// <returns>Whether the filter was uninstalled.</returns>
        public bool UninstallFilter(IVoiceFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            lock (Filters)
            {
                var filters = Filters;
                if (!filters.Contains(filter))
                    return false;

                return filters.Remove(filter);
            }
        }
    }
}
