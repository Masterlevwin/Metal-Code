using System;
using System.IO;
using System.Collections;
using ExcelDataReader;

public class Tech
{
public string Path;

public Tech(string path)
{
Path = path;
}

public string Run()
{
string notify = $"Не удается прочитать файл";
  
  using FileStream stream = File.Open(Path, FileMode.Open, FileAccess.Read);              
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

  DirectoryInfo dirLaser = Directory.CreateDirectory(Path.GetDirectoryName(Path));

return notify;
}
}
