function y = applyAirAbsorption(x,fs,fc)
% function assumes that the time sample is explicitly gives the distance
% travelled.
% Air absorption is applied in time/frequency domain as a real-time
% convolutions
%
% fc is optional, air absorption is only applied above fc.
% 
% By Alex Southern
% Virtual Acoustic Team
% Department of Media Technology
% School of Science
% Aalto University
% Espoo
% Finland

% Define Frame size (and window length)
winspan = 128;

%Get the filter length
h = fir1(100,0.99,'low');   % all pass - low pass filter
L = length(h);              % FIR filter length in taps

numcol = size(x,2);
% get input signal length and pad beginning and end
x = [zeros(winspan+L-1,numcol); x; zeros(winspan+L-1,numcol)];
signal_length = length(x);                                    

% Create Window
wintype = 0;
switch wintype
    case 0          % rectangular
        winshift = winspan;      % hop size
        win = ones(winspan,1);
    case 1          % hanning (50% overlap)
        winshift = winspan/2;    % hop size
        win = hanning(winspan,'periodic');
end

%FFT processing parameters: 
fftsize = 2^(ceil(log2(winspan+L-1)));          % FFT Length        

numFrames = 1+floor((signal_length-winspan)/winshift);    % no. complete frames
y = zeros(signal_length + fftsize,numcol);     % allocate output+'ringing' vector
specsize = fftsize/2 +1;

% FFT lowpass filter
hzp = [h'; zeros(1,fftsize-L)'];          % zero-pad h to FFT size
H = fft(hzp);                             % filter frequency response
H = H(1:specsize);

% Problem Specific Initialization
ff = ((1:specsize).*fs)/fftsize; % Air attenuation band in Hz
%--------------------------------

if nargin == 3
    % fc = absorption is only applied above this frequency (Hz)
    fci = round((fc*fftsize)/fs); %bin frequency of fc    
end


alphaFile = ['airAlpha_' num2str(numFrames) 'frames_' num2str(fftsize) 'fft.mat'];

if ~exist(alphaFile,'file')
    % Precalculate alpha
    i = 1;
    framecount = 1;
    while i + fftsize <= signal_length + fftsize && framecount <= numFrames
        alpha(:,framecount) = airAttenuationCoefficient(i+(winshift/2),fs,ff)';
        % Increment counters
        i = i + winshift;
        framecount = framecount+1;
    end
    save(['airAlpha_' num2str(numFrames) 'frames_' num2str(fftsize) 'fft.mat'],'alpha');
end
load(alphaFile);

i = 1;
framecount = 1;

if nargin == 3
    alpha(1:fci,:) = 1;
end
    
while i + fftsize <= signal_length + fftsize && framecount <= numFrames
        
    % Apply window in time domain and the take the FFT.
    %     pad_sig = [win.*x(i:(i+winspan-1),:); zeros(fftsize-winspan,numcol)];
    
    pad_sig = [bsxfun(@times,win,x(i:(i+winspan-1),:)); zeros(fftsize-winspan,numcol)];

    
    SIG = fft( pad_sig );  
    SIG = SIG(1:specsize,:); % Only need the first half           
    
    % Main Processing ---------------
   
    Z = bsxfun(@times,SIG,H.*alpha(:,framecount)); 
   
    % -------------------------------
    
    % Convert Magnitude and Phase back to Complex Signal
    %Z = mag_n.*exp(1i*ph);
    
    % Create Second Half
    tmp = complex(real(Z(2:end-1,:)),-imag(Z(2:end-1,:)));
      
    %Convert window back to Time Domain
    y(i:(i+fftsize-1),:) =   real(ifft(cat(1,Z,flipud(tmp)))) + y(i:(i+fftsize-1),:);
    
    % Increment counters
    i = i + winshift;
    framecount = framecount+1;
end

% remove pad from beginning
y = y(winspan+L:end,:);
% remove filter delay
y = y(51:end,:);


function alpha = airAttenuationCoefficient(timeframe,fs,ff)
d = 345*(timeframe/fs); % distance travelled
alpha = NormalizedAirAbsorption( d, ff );

function nab = NormalizedAirAbsorption( d, ff )

% get absorption in dB/metre
alpha1m = getAirAbsorption(ff)/304;

[q m] = size(d);
if q < m
    d = d';
end

% D = repmat( d, 1, length(alpha1m));
% A = repmat( alpha1m, length(d), 1);

DA = bsxfun(@times,d,alpha1m);

% convert to linear domain between 0 and 1
nab = exp( -(DA/20) * log(10) );

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
A = get_A(H);
T = get_T(H);
c = get_c();
W = zeros(4,length(ff));
alpha = zeros(length(ff),1);

k = 1:length(ff);

f = ff;

for i = 1:4
    X = f.*T(i).*A(i);
    Y = 1 + (f.^2) .* (T(i)^2);
    Z = X./Y;
    W(i,:) = (Z+(1.525E-9).*f);
end


alpha = 27.26*sum(W,1).*(f/c);



function plot_AandT
% The function produces same plots as in the paper
H = 0.01:0.01:100;
A(1:length(H),1:4) = 0;
T(1:length(H),1:4) = 0;

for n = 1:length(H)
    A(n,:) = get_A(H(n));
    T(n,:) = get_T(H(n));
end

h1 = figure;
h2 = figure;
for n = 1:3
    figure(h1);
    subplot(3,1,n);
    plot(H,A(:,n));
    if n == 1 
        title('Derived Relaxtion Strengths');
    end

    figure(h2);
    hold on;
    plot(H,T(:,n));
    if n == 1
        title('Modified Relaxtion Times');
    end 
end

figure(h2);
set(gca,'Yscale','log');
ylim([1E-7 2E-1]);

function A = get_A(H)
A_b = [-8.97433500000000,-0.00320434600000000,-0.000472068800000000,-0.000152587900000000;
       -7.39732400000000, 0.00617981000000000, 0.000112533600000000,-1.04904200000000e-05;
       -10.4035500000000, 0.0169830300000000,-0.00246810900000000,-0.000279426600000000;];

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


function T = get_T(H)
T_b = [-2.35700900000000,-0.542330700000000,-0.0500000000000000,5E-10;
       -5.38899200000000,-1.231140000000000,-0.0476942100000000,0.00400006800000000;
       -9.78059400000000,-0.845981000000000,-0.0339984900000000,0.00253295900000000;];

logH = log(H);

T = exp( T_b(:,1) + T_b(:,2)*logH + T_b(:,3)*(logH^2) + T_b(:,4)*(logH^3) );
T(4) = 5E-10;


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

