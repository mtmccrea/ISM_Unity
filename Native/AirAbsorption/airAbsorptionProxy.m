function val_out = airAbsorptionProxy(op_type, fs_in, ir_in) %#codegen
%AIRABSORPTIONPROXY Access point for the computation of air absorption.
    persistent cls_airAbsorption;
    if isempty(cls_airAbsorption)
        cls_airAbsorption = AirAbsorption();
    end
    val_out = ir_in;
    if strcmpi(op_type, 'setfs')
        cls_airAbsorption.setFs(fs_in);
    elseif strcmpi(op_type, 'apply')
        val_out = single(cls_airAbsorption.apply(ir_in));
        val_out = val_out(1 : size(ir_in, 1));
    end
end

