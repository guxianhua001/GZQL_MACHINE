using System;
using System.Collections.Generic;
using Core.Services;
class P {
  static void Main() {
    var cad = new List<(double Cx,double Cy)> { (0,0), (100,0), (0,100), (100,100) };
    var mach = new List<(double Mx,double My)> { (10,20), (110,20), (10,120), (111,21) };
    var r = AffineCalibrationService.Solve(cad, mach);
    Console.WriteLine("n=4 RMS=" + r.RmsError.ToString("F6"));
    for(int i=0;i<cad.Count;i++){
      var t = AffineCalibrationService.Transform(r,cad[i].Cx,cad[i].Cy);
      double dx=t.Mx-mach[i].Mx, dy=t.My-mach[i].My;
      Console.WriteLine("  res=" + Math.Sqrt(dx*dx+dy*dy).ToString("F6"));
    }
    var cad2 = new List<(double Cx,double Cy)> { (0,0), (50,10), (20,80), (90,70) };
    var mach2 = new List<(double Mx,double My)> { (1,2), (51.5,12), (21,82.5), (92,68) };
    var r2 = AffineCalibrationService.Solve(cad2, mach2);
    Console.WriteLine("asym RMS=" + r2.RmsError.ToString("F6") + " res=[" + string.Join(",", r2.Residuals) + "]");
    var cad3 = cad.GetRange(0,3); var mach3 = mach.GetRange(0,3);
    var r3 = AffineCalibrationService.Solve(cad3, mach3);
    Console.WriteLine("n=3 RMS=" + r3.RmsError.ToString("F6"));
  }
}
