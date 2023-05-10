// Copyright (c) Microsoft. All rights reserved.

import { makeStyles, shorthands, tokens } from '@fluentui/react-components';
import debug from 'debug';
import React from 'react';
import { Constants } from '../../Constants';
import { AuthorRoles } from '../../libs/models/ChatMessage';
import { useChat } from '../../libs/useChat';
import { useAppDispatch, useAppSelector } from '../../redux/app/hooks';
import { RootState } from '../../redux/app/store';
import { updateConversation } from '../../redux/features/conversations/conversationsSlice';
import { ChatHistory } from './ChatHistory';
import { ChatInput } from './ChatInput';

const log = debug(Constants.debug.root).extend('chat-room');

const useClasses = makeStyles({
    root: {
        height: '94.5%',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        gridTemplateColumns: '1fr',
        gridTemplateRows: '1fr auto',
        gridTemplateAreas: "'history' 'input'",
    },
    history: {
        ...shorthands.gridArea('history'),
        ...shorthands.padding(tokens.spacingVerticalM),
        overflowY: 'auto',
        display: 'grid',
    },
    input: {
        ...shorthands.gridArea('input'),
        ...shorthands.padding(tokens.spacingVerticalM),
        backgroundColor: tokens.colorNeutralBackground4,
    },
});

export const ChatRoom: React.FC = () => {
    const { conversations, selectedId } = useAppSelector((state: RootState) => state.conversations);
    const { audience } = conversations[selectedId];
    const messages = conversations[selectedId].messages;
    const classes = useClasses();

    const dispatch = useAppDispatch();
    const scrollViewTargetRef = React.useRef<HTMLDivElement>(null);
    const scrollTargetRef = React.useRef<HTMLDivElement>(null);
    const [shouldAutoScroll, setShouldAutoScroll] = React.useState(true);

    // hardcode to care only about the bot typing for now.
    const [isBotTyping, setIsBotTyping] = React.useState(false);

    const chat = useChat();

    React.useEffect(() => {
        if (!shouldAutoScroll) return;
        scrollToTarget(scrollTargetRef.current);
    }, [messages, audience, shouldAutoScroll]);

    React.useEffect(() => {
        const onScroll = () => {
            if (!scrollViewTargetRef.current) return;
            const { scrollTop, scrollHeight, clientHeight } = scrollViewTargetRef.current;
            const isAtBottom = scrollTop + clientHeight >= scrollHeight - 10;
            setShouldAutoScroll(isAtBottom);
        };

        if (!scrollViewTargetRef.current) return;

        const currentScrollViewTarget = scrollViewTargetRef.current;

        currentScrollViewTarget.addEventListener('scroll', onScroll);
        return () => {
            currentScrollViewTarget.removeEventListener('scroll', onScroll);
        };
    }, []);

    const handleSubmit = async (value: string) => {
        log('submitting user chat message');
        const chatInput = {
            timestamp: new Date().getTime(),
            userId: Constants.guestUser.id,
            userName: Constants.guestUser.name,
            content: value,
            authorRole: AuthorRoles.User,
        };
        setIsBotTyping(true);
        dispatch(updateConversation({ message: chatInput }));
        try {
            await chat.getResponse(value, selectedId);
        } finally {
            setIsBotTyping(false);
        }
        setShouldAutoScroll(true);
    };

    return (
        <div className={classes.root}>
            <div ref={scrollViewTargetRef} className={classes.history}>
                <ChatHistory audience={audience} messages={messages} />
                <div>
                    <div ref={scrollTargetRef} />
                </div>
            </div>
            <div className={classes.input}>
                <ChatInput isTyping={isBotTyping} onSubmit={handleSubmit} />
            </div>
        </div>
    );
};

const scrollToTarget = (element: HTMLElement | null) => {
    if (!element) return;
    element.scrollIntoView({ block: 'start', behavior: 'smooth' });
};