namespace csharp Example1.Thrift

struct VoidResp
{
}

struct BooleanStruct
{
    1:bool Value;
}

struct StringStruct
{
    1:string Value;
}

struct SetReq
{
    1:string Key;
    2:string Value;
}

struct GetReq
{
    1:string Key;
}

struct RemoveReq
{
    1:string Key;
}