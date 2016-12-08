namespace csharp SharpOTP.Remote.Messaging

/**
 * message action
 */
enum Actions
{
	Request					= 0,
	Response				= 1,
}

/**
 * remoting exception
 */
exception RemotingException
{
	1:i32 ErrorCode;						//error code
	2:string Error;							//error
}

/**
 * remote message
 */
struct Message
{
	2:Actions Action;						//action
	3:string MethodName;					//method name
	4:binary Payload;						//message body
	5:RemotingException Exception;			//exception
	7:string ReplyTo;						//reply to queue name.
	8:i64 CorrentionId;						//reply to corrention Id
    9:list<binary> ListPayload;             //payload list
    20:string To;                           //to queue name.
    21:i64 CreatedTick;                     //created time ticks(注：此字段不会序列化)
    22:i32 MillisecondsTimeout;             //超时时间
}

/**
 * boolean
 */
struct Boolean
{
	1:bool Value;							//value
}