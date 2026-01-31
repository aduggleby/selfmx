import { useState } from 'react';
import { toast } from 'sonner';
import { Send, X } from 'lucide-react';
import { useSendTestEmail } from '@/hooks/useDomains';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

interface SendTestEmailFormProps {
  domainId: string;
  domainName: string;
  onClose: () => void;
}

export function SendTestEmailForm({ domainId, domainName, onClose }: SendTestEmailFormProps) {
  const [senderPrefix, setSenderPrefix] = useState('test');
  const [to, setTo] = useState('');
  const [subject, setSubject] = useState('Test email from SelfMX');
  const [text, setText] = useState('This is a test email sent from SelfMX.');

  const sendTestEmailMutation = useSendTestEmail();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    try {
      const result = await sendTestEmailMutation.mutateAsync({
        domainId,
        senderPrefix,
        to,
        subject,
        text,
      });
      const truncatedId = result.id.length > 20 ? `${result.id.slice(0, 20)}...` : result.id;
      toast.success(`Email sent! Message ID: ${truncatedId}`);
      onClose();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to send test email');
    }
  };

  return (
    <Card className="border-border/70 mt-6">
      <CardHeader className="pb-4">
        <div className="flex items-center justify-between">
          <CardTitle className="text-lg">Send Test Email</CardTitle>
          <Button variant="ghost" size="icon" onClick={onClose} className="h-8 w-8">
            <X className="h-4 w-4" />
          </Button>
        </div>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <label className="text-sm font-medium text-foreground">From</label>
            <div className="flex items-center gap-0">
              <Input
                value={senderPrefix}
                onChange={(e) => setSenderPrefix(e.target.value)}
                placeholder="test"
                className="rounded-r-none border-r-0 flex-1"
                required
              />
              <div className="h-11 px-4 flex items-center bg-muted/50 border border-input rounded-r-2xl text-muted-foreground text-sm">
                @{domainName}
              </div>
            </div>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium text-foreground">To</label>
            <Input
              type="email"
              value={to}
              onChange={(e) => setTo(e.target.value)}
              placeholder="recipient@example.com"
              required
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium text-foreground">Subject</label>
            <Input
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              placeholder="Subject"
              required
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium text-foreground">Message</label>
            <Textarea
              value={text}
              onChange={(e) => setText(e.target.value)}
              placeholder="Your message..."
              rows={4}
              required
            />
          </div>

          <div className="flex justify-end gap-3 pt-2">
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={sendTestEmailMutation.isPending}>
              <Send className="h-4 w-4 mr-2" />
              {sendTestEmailMutation.isPending ? 'Sending...' : 'Send Test Email'}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
