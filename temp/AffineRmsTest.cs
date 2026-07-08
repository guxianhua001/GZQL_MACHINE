using System;
using System.Collections.Generic;
using Core.Services;

var cad = new List<(double,double)> { (0,0), (100,0), (0,100), (100,100) };
var mach = new List<(double,double)> { (10,20), (110,20), (10,120), (111,21) }; // 4th point 1mm off in x,y
var r = AffineCalibrationService.Solve(cad, mach);
Console.WriteLine($"n={r.PointCount} RMS={r.RmsError:F6} Residuals=[{string.Join(",", r.Residuals)}]");

var cad3 = cad.GetRange(0,3);
var mach3 = mach.GetRange(0,3);
var r3 = AffineCalibrationService.Solve(cad3, mach3);
Console.WriteLine($"n=3 RMS={r3.RmsError:F6} Residuals=[{string.Join(",", r3.Residuals)}]");
