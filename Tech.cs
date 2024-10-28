using System;
using System.IO;
using Path = System.IO.Path;
using System.Collections.Generic;
using ExcelDataReader;

public class Tech
{
public string ExcelFile;

public Tech(string path)
{
ExcelFile = path;
}

public string Run()
{
string notify = $"Не удается прочитать файл";
  
  using FileStream stream = File.Open(ExcelFile, FileMode.Open, FileAccess.Read);              
  using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);             
  DataSet result = reader.AsDataSet();              
  DataTable table = result.Tables[0];

  List<string> names = new();
  for (int i = 1; i < table.Rows.Count; i++)
  {
    names.Add($"{table.Rows[i].ItemArray[1]} "
              + "{table.Rows[i].ItemArray[2]} " 
              + "s{table.Rows[i].ItemArray[3]} "
              + "n{table.Rows[i].ItemArray[4]}");
  }

  string[] files = Directory.GetFiles(Path.GetDirectoryName(ExcelFile));
  
  DirectoryInfo dirLaser = Directory.CreateDirectory(
    Path.GetDirectoryName(ExcelFile) + "\\" + "Лазер");
  
  foreach (string name in names)
    foreach (string file in files)
      if (name.Contains(file))
      {
        file.Copy(dir + "\\" + $"{name}");
        break;
      }

return notify;
}
}
