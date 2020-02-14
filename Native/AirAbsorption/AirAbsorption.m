classdef AirAbsorption < handle %#codegen
    %AIRABSORPTION Summary of this class goes here
    %   Detailed explanation goes here
    
    properties (Access=public)
        fs;
        win;
        fftsize;
        specsize;
        H;
        ff;
        numFrames;
        L;
        padsize;
    end
    
    properties (Constant)
        winlength = 128;
        winshift = AirAbsorption.winlength / 2;
    end
    
    methods (Access=public)
        
        function obj = AirAbsorption()
            obj.fs = uint32(0);
            obj.win = single(hanning(AirAbsorption.winlength,'periodic'));
            % Get the filter length
            h = single(fir1(100,0.99,'low'));         % all pass - low pass filter
            obj.L = uint32(length(h));                    % FIR filter length in taps
            obj.padsize = uint32(AirAbsorption.winlength + obj.L - 1);
            %FFT processing parameters: 
            obj.fftsize = uint32(2^(ceil(log2(double(obj.padsize)))));  % FFT Length        
            obj.specsize = uint32(obj.fftsize/2 + 1);
            % FFT lowpass filter
            hzp = [h'; single(zeros(1,obj.fftsize-obj.L)')];  % zero-pad h to FFT size
            H = fft(hzp);                 % filter frequency response
            obj.H = H(1:obj.specsize);
            obj.ff = single(1:obj.specsize)/single(obj.fftsize);
            obj.numFrames = uint32(0);
        end
        
        
        function setFs(obj, fs)
            obj.fs = fs;
            % Problem Specific Initialization
            obj.numFrames = uint32(0);
        end
        
        
        function ir_out = apply(obj, ir_in)
            persistent alpha;
            if isempty(alpha)
                alpha = zeros(obj.specsize, obj.numFrames);
            end
            % get input signal length and pad beginning and end
            orig_length = size(ir_in, 1);
            ir_in = [zeros(obj.padsize,1); ir_in; zeros(obj.padsize,1)];
            signal_length = length(ir_in);
            %FFT processing parameters
            num_frames = uint32(1+floor((signal_length-AirAbsorption.winlength)/AirAbsorption.winshift));
            % Precalculate alpha (if necessary)
            if obj.numFrames < num_frames
                % preallocate alpha buffer
                if size(alpha, 2) == 0
                    alpha = zeros(obj.specsize, num_frames);
                else
                    alpha = [alpha, zeros(obj.specsize, num_frames - obj.numFrames)];
                end
                % create new filters
                i = obj.numFrames * AirAbsorption.winshift + 1;
                framecount = obj.numFrames + 1;
                ff_ = obj.ff .* single(obj.fs); 
                while i <= signal_length && framecount <= num_frames
                    alpha(:,framecount) = ...
                        AirAbsorption.airAttenuationCoefficient( ...
                            single(i + (AirAbsorption.winshift / 2)), ...
                            single(obj.fs), ...
                            ff_)';
                    % Increment counters
                    i = i + AirAbsorption.winshift;
                    framecount = framecount+1;
                end
                obj.numFrames = num_frames;
            end
            i = 1;
            framecount = 1;
            y = zeros(signal_length + obj.fftsize, 1);
            while i + AirAbsorption.winlength <= signal_length && framecount <= obj.numFrames
                % Apply window in time domain and the take the FFT.
                idx_in = i:(i + AirAbsorption.winlength - 1);
                SIG = fft(bsxfun(@times, obj.win, ir_in(idx_in)), ...
                          obj.fftsize); 
                SIG = SIG(1 : obj.specsize, :); % Only need the first half           
                % Main Processing ---------------
                Z = bsxfun(@times, SIG, obj.H .* alpha(:, framecount)); 
                % -------------------------------
                tmp = real(ifft([Z; flipud(conj(Z(2:end-1)))], [], 1));
                %Convert window back to Time Domain
                idx_out = i : (i + obj.fftsize - 1);
                y(idx_out) = tmp + y(idx_out);
                % Increment counters
                i = i + AirAbsorption.winshift;
                framecount = framecount + 1;
            end
            % remove pad from beginning & filter delay
            cp_start_offset = obj.padsize + 51;
            ir_out = y(cp_start_offset : (cp_start_offset + orig_length));
%             % remove filter delay
%             ir_out = ir_out(51:end,:);
        end
        
    end
    
    
    methods (Access=private, Static=true)
        
        function alpha = airAttenuationCoefficient(timeframe,fs,ff)
            d = 345*(timeframe/fs); % distance travelled
            alpha = AirAbsorption.NormalizedAirAbsorption( d, ff );
        end

        
        function nab = NormalizedAirAbsorption( d, ff )
            % get absorption in dB/metre
            alpha1m = AirAbsorption.getAirAbsorption(ff)/304;
            [q, m] = size(d);
            if q < m
                d = d';
            end
            % D = repmat( d, 1, length(alpha1m));
            % A = repmat( alpha1m, length(d), 1);
            DA = bsxfun(@times,double(d),alpha1m);
            % convert to linear domain between 0 and 1
            nab = exp( -(DA/20) * log(10) );
        end

        
        function alpha = getAirAbsorption(ff)
            % Function to correcly return the absorption of sound in still air at 20C 
            % and is valid in the frequency range 12Hz - 1MHz over all humidities as 
            % given by Bass et al. 
            % JASA Feb 1972 - Atmospheric Absorption of Sound: Analytical Expressions
            %
            % NOTE: The authors have made more recent papers which are not taken to
            % account in this script.
            %
            %   Usage:
            %   ff =    The frequency of interest in Hz- this may be 
            %           a vector of frequencies.
            %   alpha = absorption is given as dB of attenuation for 
            %           each ff over 304m (1000ft).
            %
            % Implemented by, 
            % A. Southern, 
            % Virtual Acoustics Team
            % Dept of Media Technology, 
            % Aalto University, March 2012

            % Note: Denoising Code
            % http://homepage.univie.ac.at/moika.doerfler/StrucAudio.html
            if nargin == 0
                ff = 20:20000;
            end
            H = 45; % Percentage Humidity
            A = AirAbsorption.get_A(H);
            T = AirAbsorption.get_T(H);
            c = AirAbsorption.get_c();
            W = zeros(4,length(ff));
%             alpha = zeros(length(ff),1);
%             k = 1:length(ff);
            f = ff;
            for i = 1:4
                X = f.*T(i).*A(i);
                Y = 1 + (f.^2) .* (T(i)^2);
                Z = X./Y;
                W(i,:) = (Z+(1.525E-9).*f);
            end
            alpha = 27.26*sum(W,1).*(f/c);
        end
        
        
        function A = get_A(H)
            A_b = [-8.97433500000000,-0.00320434600000000,-0.000472068800000000,-0.000152587900000000;
                   -7.39732400000000, 0.00617981000000000, 0.000112533600000000,-1.04904200000000e-05;
                   -10.4035500000000, 0.0169830300000000,-0.00246810900000000,-0.000279426600000000;
                   0, 0, 0, 0];
            % for i = 1:3
            %     b0 = A_b(i,1);
            %     b1 = A_b(i,2);
            %     b2 = A_b(i,3);
            %     b3 = A_b(i,4);    
            %     A(i) = exp( A_b(i,1) + A_b(i,2)*log(H) + A_b(i,3)*(log(H)^2) + A_b(i,4)*(log(H)^3));
            % end
            logH = log(H);
            A = exp( A_b(:,1) + A_b(:,2)*logH + A_b(:,3)*(logH^2) + A_b(:,4)*(logH^3));
            A(4) = 1;
        end

        
        function T = get_T(H)
            T_b = [-2.35700900000000,-0.542330700000000,-0.0500000000000000,5E-10;
                   -5.38899200000000,-1.231140000000000,-0.0476942100000000,0.00400006800000000;
                   -9.78059400000000,-0.845981000000000,-0.0339984900000000,0.00253295900000000;
                   0, 0, 0, 0];
            logH = log(H);
            T = exp( T_b(:,1) + T_b(:,2)*logH + T_b(:,3)*(logH^2) + T_b(:,4)*(logH^3) );
            T(4) = 5E-10;
        end

        
        function c = get_c(Pw,H,theta)
            if nargin == 0
                c = 1.13;
            else
                h = Pw*H/100;
                F = 28.966-10.95*h;
                E = 8.3166E7 * theta;
                D = (E / F);
                C = 2.5 + 5*h;
                B = 3.5 + 5*h;
                A = ( (B/C)*D ); 
                c = (1/30480)*( A^0.5 );
            end
        end
        
    end
end

