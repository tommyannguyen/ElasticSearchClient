using System.Security.Cryptography.X509Certificates;
using System;
using System.Collections.Generic;
public class Entity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<TagLog> Tags { get; set; }
}

public class TagLog
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<string> Tags { get; set; }
    public DateTime CreationDate { get; set; }
}