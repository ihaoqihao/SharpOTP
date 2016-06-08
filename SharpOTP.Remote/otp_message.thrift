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
	3:string Code;							//message code
	4:binary Payload;						//message body
	5:RemotingException Exception;			//exception
	6:string To;							//to queue name.
	7:string ReplyTo;						//reply to queue name.
	8:i64 CorrentionId;						//reply to corrention Id
}

/**
 * boolean
 */
struct Boolean
{
	1:bool Value;							//value
}