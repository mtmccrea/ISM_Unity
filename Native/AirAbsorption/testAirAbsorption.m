fs = uint32(48000);
ir = single(linspace(1, 0, fs)' .* (rand(fs, 1) - 0.5) * 2);
% ir = single((rand(fs, 1) - 0.5) * 2);
n_runs = 10;
fprintf('Setting fs...'); tic;
airAbsorptionProxy('setFs', fs, single(zeros(0,1)));
fprintf(' Done.'); toc;
fprintf('Testing air absorption...\n');
fprintf('First round...'); tic;
ir_out = airAbsorptionProxy('apply', uint32(0), ir);
fprintf(' Done. '); toc;
fprintf('Calculate average running time...'); t_start = tic;
for i = 1 : n_runs
    ir_out = airAbsorptionProxy('apply', uint32(0), ir);
end
fprintf(' Done. '); t_full = toc(t_start);
fprintf('Average running time was %f seconds.\n', t_full / n_runs);
plot([[ir; zeros(length(ir_out)-length(ir), 1)], ir_out]);
legend({'Original', 'With air absorption'})