import { useState } from 'react';
import { toast } from 'sonner';
import { Send, X } from 'lucide-react';
import { useSendTestEmail } from '@/hooks/useDomains';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';

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
    <div className="rounded border bg-muted/30 p-4">
      <div className="flex items-center justify-between mb-4">
        <h4 className="text-sm font-medium">Send Test Email</h4>
        <Button variant="ghost" size="sm" onClick={onClose} className="h-6 w-6 p-0">
          <X className="h-3.5 w-3.5" />
        </Button>
      </div>
      <form onSubmit={handleSubmit} className="space-y-3">
        <div>
          <label className="text-xs text-muted-foreground">From</label>
          <div className="flex items-center gap-0 mt-1">
            <Input
              value={senderPrefix}
              onChange={(e) => setSenderPrefix(e.target.value)}
              placeholder="test"
              className="rounded-r-none border-r-0 flex-1 h-8 text-sm"
              required
            />
            <div className="h-8 px-3 flex items-center bg-muted/50 border border-input rounded-r-md text-muted-foreground text-xs font-mono">
              @{domainName}
            </div>
          </div>
        </div>

        <div>
          <label className="text-xs text-muted-foreground">To</label>
          <Input
            type="email"
            value={to}
            onChange={(e) => setTo(e.target.value)}
            placeholder="recipient@example.com"
            className="mt-1 h-8 text-sm"
            required
          />
        </div>

        <div>
          <label className="text-xs text-muted-foreground">Subject</label>
          <Input
            value={subject}
            onChange={(e) => setSubject(e.target.value)}
            placeholder="Subject"
            className="mt-1 h-8 text-sm"
            required
          />
        </div>

        <div>
          <label className="text-xs text-muted-foreground">Message</label>
          <Textarea
            value={text}
            onChange={(e) => setText(e.target.value)}
            placeholder="Your message..."
            rows={3}
            className="mt-1 text-sm"
            required
          />
        </div>

        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" size="sm" disabled={sendTestEmailMutation.isPending}>
            <Send className="h-3.5 w-3.5 mr-1.5" />
            {sendTestEmailMutation.isPending ? 'Sending...' : 'Send'}
          </Button>
        </div>
      </form>
    </div>
  );
}
